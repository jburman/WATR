using System.Numerics.Tensors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WATR.Shared.Models;
using WATR.Web.Services;
using Xunit;

namespace WATR.Tests.Services;

public class OnnxEmbeddingServiceTests
{
    private class DummyHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "WATR.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    [Fact]
    public void VectorSerialization_ShouldRoundTrip()
    {
        var service = new OnnxEmbeddingService(
            new DummyHostEnvironment(),
            NullLogger<OnnxEmbeddingService>.Instance,
            new DummyHttpClientFactory());

        float[] original = [0.123f, -0.456f, 0.789f, 1.0f, -1.0f, 0.0f];
        byte[] blob = service.SerializeVector(original);

        Assert.Equal(original.Length * sizeof(float), blob.Length);

        ReadOnlySpan<float> deserialized = service.DeserializeVector(blob);
        Assert.Equal(original.Length, deserialized.Length);

        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], deserialized[i]);
        }
    }

    [Fact]
    public void CosineSimilarity_OrthogonalAndParallelVectors_CalculatesCorrectly()
    {
        float[] v1 = [1.0f, 0.0f, 0.0f];
        float[] v2 = [1.0f, 0.0f, 0.0f];
        float[] v3 = [0.0f, 1.0f, 0.0f];
        float[] v4 = [-1.0f, 0.0f, 0.0f];

        float simSame = TensorPrimitives.CosineSimilarity(v1, v2);
        float simOrthogonal = TensorPrimitives.CosineSimilarity(v1, v3);
        float simOpposite = TensorPrimitives.CosineSimilarity(v1, v4);

        Assert.Equal(1.0f, simSame, precision: 4);
        Assert.Equal(0.0f, simOrthogonal, precision: 4);
        Assert.Equal(-1.0f, simOpposite, precision: 4);
    }

    [Fact]
    public void BuildCompositeTaskText_ShouldCombineTitleBodyAndNotes()
    {
        var service = new OnnxEmbeddingService(
            new DummyHostEnvironment(),
            NullLogger<OnnxEmbeddingService>.Instance,
            new DummyHttpClientFactory());

        var task = new TaskItem
        {
            Title = "Fix database latency",
            Body = "Investigate slow SQLite queries on large datasets.",
            Notes = new List<TaskNote>
            {
                new() { Content = "Indexed the CreatedAt column." },
                new() { Content = "Benchmark completed with 50% improvement." }
            }
        };

        string text = service.BuildCompositeTaskText(task);

        Assert.Contains("Fix database latency", text);
        Assert.Contains("Investigate slow SQLite queries", text);
        Assert.Contains("Indexed the CreatedAt column.", text);
        Assert.Contains("Benchmark completed with 50% improvement.", text);
    }
}
