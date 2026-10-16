using Microsoft.EntityFrameworkCore;
using WATR.Web.Data;

namespace WATR.Web.Services;

public class TaskEmbeddingWorker : BackgroundService
{
    private readonly TaskEmbeddingQueue _queue;
    private readonly IOnnxEmbeddingService _embeddingService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskEmbeddingWorker> _logger;

    public TaskEmbeddingWorker(
        TaskEmbeddingQueue queue,
        IOnnxEmbeddingService embeddingService,
        IServiceScopeFactory scopeFactory,
        ILogger<TaskEmbeddingWorker> logger)
    {
        _queue = queue;
        _embeddingService = embeddingService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskEmbeddingWorker background service started.");

        // Launch periodic sweep in background
        _ = PeriodicSweepAsync(stoppingToken);

        // Process incoming items from queue
        try
        {
            await foreach (var taskId in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessTaskEmbeddingAsync(taskId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in TaskEmbeddingWorker queue consumer.");
        }
    }

    private async Task ProcessTaskEmbeddingAsync(Guid taskId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WatrDbContext>();

            var task = await db.TaskItems
                .Include(t => t.Notes)
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

            if (task == null) return;

            // If already up-to-date, skip
            if (task.Embedding != null && task.EmbeddingUpdatedAt.HasValue && task.EmbeddingUpdatedAt.Value >= task.UpdatedAt)
            {
                return;
            }

            var text = _embeddingService.BuildCompositeTaskText(task);
            var vector = await _embeddingService.GenerateEmbeddingVectorAsync(text, cancellationToken);

            task.Embedding = _embeddingService.SerializeVector(vector);
            task.EmbeddingUpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Generated embedding for task {TaskId} ({Handle}).", task.Id, task.TaskHandle);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute embedding for task {TaskId}.", taskId);
        }
    }

    private async Task PeriodicSweepAsync(CancellationToken stoppingToken)
    {
        // Initial warm-up delay so app startup is completely fast and unhindered
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WatrDbContext>();

                var unindexedIds = await db.TaskItems
                    .Where(t => t.Embedding == null || !t.EmbeddingUpdatedAt.HasValue || t.EmbeddingUpdatedAt.Value < t.UpdatedAt)
                    .Select(t => t.Id)
                    .ToListAsync(stoppingToken);

                if (unindexedIds.Count > 0)
                {
                    _logger.LogInformation("Sweep found {Count} unindexed/outdated tasks for embedding.", unindexedIds.Count);
                    foreach (var id in unindexedIds)
                    {
                        _queue.Enqueue(id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic task embedding sweep.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
