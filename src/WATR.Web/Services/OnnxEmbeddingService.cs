using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using WATR.Shared.Models;

namespace WATR.Web.Services;

public interface IOnnxEmbeddingService : IEmbeddingGenerator<string, Embedding<float>>
{
    Task<float[]> GenerateEmbeddingVectorAsync(string text, CancellationToken cancellationToken = default);
    byte[] SerializeVector(ReadOnlySpan<float> vector);
    ReadOnlySpan<float> DeserializeVector(byte[] blob);
    string BuildCompositeTaskText(TaskItem task);
}

public class OnnxEmbeddingService : IOnnxEmbeddingService
{
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";
    private const int MaxSequenceLength = 256;
    private const int ChunkOverlapTokens = 32;
    private const int MaxChunks = 16;
    private const int EmbeddingDimension = 384;

    private readonly IHostEnvironment _env;
    private readonly ILogger<OnnxEmbeddingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private bool _initialized;

    public OnnxEmbeddingService(
        IHostEnvironment env,
        ILogger<OnnxEmbeddingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _env = env;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ModelDownloader");
    }

    public EmbeddingGeneratorMetadata Metadata { get; } =
        new("HuggingFace", new Uri("https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2"), "all-MiniLM-L6-v2", EmbeddingDimension);

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            var dataRoot = ResolvePersistentDataRoot(_env.ContentRootPath);
            var modelDir = Path.Combine(dataRoot, "models", "all-MiniLM-L6-v2");
            Directory.CreateDirectory(modelDir);

            var modelPath = Path.Combine(modelDir, "model.onnx");
            var vocabPath = Path.Combine(modelDir, "vocab.txt");

            if (!File.Exists(vocabPath))
            {
                _logger.LogInformation("Downloading embedding vocab from {Url} to {Path}...", VocabUrl, vocabPath);
                var tempVocab = vocabPath + ".tmp";
                using (var stream = await _httpClient.GetStreamAsync(VocabUrl, cancellationToken))
                using (var file = File.Create(tempVocab))
                {
                    await stream.CopyToAsync(file, cancellationToken);
                }
                File.Move(tempVocab, vocabPath, overwrite: true);
                _logger.LogInformation("Vocab downloaded successfully.");
            }

            if (!File.Exists(modelPath))
            {
                _logger.LogInformation("Downloading ONNX model from {Url} to {Path} (~90MB)...", ModelUrl, modelPath);
                var tempModel = modelPath + ".tmp";
                using (var stream = await _httpClient.GetStreamAsync(ModelUrl, cancellationToken))
                using (var file = File.Create(tempModel))
                {
                    await stream.CopyToAsync(file, cancellationToken);
                }
                File.Move(tempModel, modelPath, overwrite: true);
                _logger.LogInformation("ONNX model downloaded successfully.");
            }

            _logger.LogInformation("Initializing ONNX InferenceSession and BertTokenizer...");
            var bertOptions = new BertOptions
            {
                LowerCaseBeforeTokenization = true
            };
            _tokenizer = BertTokenizer.Create(vocabPath, bertOptions);

            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
            sessionOptions.AppendExecutionProvider_CPU(0);
            sessionOptions.IntraOpNumThreads = Math.Min(Environment.ProcessorCount, 4);
            _session = new InferenceSession(modelPath, sessionOptions);

            _initialized = true;
            _logger.LogInformation("ONNX Embedding Service initialized successfully.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<float[]> GenerateEmbeddingVectorAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[EmbeddingDimension];
        }

        await EnsureInitializedAsync(cancellationToken);

        var contentTokenIds = _tokenizer!.EncodeToIds(text, addSpecialTokens: false, considerPreTokenization: true, considerNormalization: true);
        if (contentTokenIds.Count == 0)
        {
            return new float[EmbeddingDimension];
        }

        int maxPayloadTokens = MaxSequenceLength - 2; // Reserve [CLS] (101) and [SEP] (102)
        if (contentTokenIds.Count <= maxPayloadTokens)
        {
            return ComputeSingleChunkEmbedding(contentTokenIds, 0, contentTokenIds.Count);
        }

        // Multi-chunk sliding window mean pooling for long task bodies and decision notes
        var combinedEmbedding = new float[EmbeddingDimension];
        int stride = Math.Max(1, maxPayloadTokens - ChunkOverlapTokens);
        int chunkCount = 0;

        for (int start = 0; start < contentTokenIds.Count && chunkCount < MaxChunks; start += stride)
        {
            int length = Math.Min(maxPayloadTokens, contentTokenIds.Count - start);
            var chunkVector = ComputeSingleChunkEmbedding(contentTokenIds, start, length);
            for (int d = 0; d < EmbeddingDimension; d++)
            {
                combinedEmbedding[d] += chunkVector[d];
            }
            chunkCount++;

            if (start + length >= contentTokenIds.Count)
            {
                break;
            }
        }

        var combinedSpan = combinedEmbedding.AsSpan();
        var combinedNorm = TensorPrimitives.Norm(combinedSpan);
        if (combinedNorm > 0)
        {
            TensorPrimitives.Divide(combinedSpan, combinedNorm, combinedSpan);
        }

        return combinedEmbedding;
    }

    private float[] ComputeSingleChunkEmbedding(IReadOnlyList<int> contentTokenIds, int start, int length)
    {
        int count = length + 2;
        var inputIds = new long[count];
        var attentionMask = new long[count];
        var tokenTypeIds = new long[count];

        inputIds[0] = 101; // [CLS]
        attentionMask[0] = 1;
        for (int i = 0; i < length; i++)
        {
            inputIds[i + 1] = contentTokenIds[start + i];
            attentionMask[i + 1] = 1;
        }
        inputIds[count - 1] = 102; // [SEP]
        attentionMask[count - 1] = 1;

        var inputIdsTensor = new DenseTensor<long>(inputIds.AsMemory(), new[] { 1, count });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask.AsMemory(), new[] { 1, count });
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds.AsMemory(), new[] { 1, count });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _session!.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        // Attention-masked mean pooling
        var embedding = new float[EmbeddingDimension];
        int maskCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (attentionMask[i] == 1)
            {
                maskCount++;
                for (int d = 0; d < EmbeddingDimension; d++)
                {
                    embedding[d] += lastHiddenState[0, i, d];
                }
            }
        }

        if (maskCount > 0)
        {
            for (int d = 0; d < EmbeddingDimension; d++)
            {
                embedding[d] /= maskCount;
            }
        }

        // L2 Normalization
        var span = embedding.AsSpan();
        var norm = TensorPrimitives.Norm(span);
        if (norm > 0)
        {
            TensorPrimitives.Divide(span, norm, span);
        }

        return embedding;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<Embedding<float>>();
        foreach (var value in values)
        {
            var vector = await GenerateEmbeddingVectorAsync(value, cancellationToken);
            embeddings.Add(new Embedding<float>(vector));
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public byte[] SerializeVector(ReadOnlySpan<float> vector) =>
        MemoryMarshal.AsBytes(vector).ToArray();

    public ReadOnlySpan<float> DeserializeVector(byte[] blob) =>
        MemoryMarshal.Cast<byte, float>(blob.AsSpan());

    public string BuildCompositeTaskText(TaskItem task)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{task.TaskType ?? "Task"}] {task.TaskHandle}: {task.Title}");
        if (task.Tags != null && task.Tags.Count > 0)
        {
            sb.AppendLine($"Tags: {string.Join(", ", task.Tags)}");
        }
        if (task.SupersedesHandles != null && task.SupersedesHandles.Count > 0)
        {
            sb.AppendLine($"Supersedes: {string.Join(", ", task.SupersedesHandles)}");
        }
        if (task.Status == WATR.Shared.Enums.TaskItemStatus.Obsolete)
        {
            sb.AppendLine("Status: Obsolete");
        }
        if (!string.IsNullOrWhiteSpace(task.SupersededByHandle))
        {
            sb.AppendLine($"Superseded by: {task.SupersededByHandle}");
        }
        if (!string.IsNullOrWhiteSpace(task.Body))
        {
            sb.AppendLine();
            sb.AppendLine(task.Body);
        }
        if (task.Notes != null && task.Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Notes:");
            foreach (var note in task.Notes.OrderBy(n => n.CreatedAt))
            {
                sb.AppendLine($"- {note.Content}");
            }
        }
        return sb.ToString();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    private static string ResolvePersistentDataRoot(string contentRootPath)
    {
        var isAzureAppService =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        var homeDir = Environment.GetEnvironmentVariable("HOME");

        if (isAzureAppService && !string.IsNullOrEmpty(homeDir))
        {
            return Path.Combine(homeDir, "data");
        }

        return Path.Combine(contentRootPath, "App_Data");
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}
