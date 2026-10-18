using System.Numerics.Tensors;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using WATR.Shared.Models;
using WATR.Web.Data;
using WATR.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace WATR.Web.Services;

public class TaskService
{
    private readonly IDbContextFactory<WatrDbContext> _dbFactory;
    private readonly IHubContext<TaskHub> _hubContext;
    private readonly ILogger<TaskService> _logger;
    private readonly TaskEmbeddingQueue _embeddingQueue;
    private readonly IOnnxEmbeddingService _embeddingService;

    public TaskService(
        IDbContextFactory<WatrDbContext> dbFactory,
        IHubContext<TaskHub> hubContext,
        ILogger<TaskService> logger,
        TaskEmbeddingQueue embeddingQueue,
        IOnnxEmbeddingService embeddingService)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
        _logger = logger;
        _embeddingQueue = embeddingQueue;
        _embeddingService = embeddingService;
    }

    public async Task<PaginatedResponse<TaskItemSummaryDto>> GetTasksAsync(
        Guid projectId,
        IEnumerable<TaskItemStatus>? statuses,
        TaskPriority? priorityFilter,
        bool? myTasksOnly,
        Guid currentUserId,
        int page,
        int pageSize,
        string? taskTypeFilter = null,
        string? tagFilter = null)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        if (!await VerifyMembershipAsync(db, projectId, currentUserId))
        {
            _logger.LogWarning("User {UserId} attempted to access project {ProjectId} without membership", currentUserId, projectId);
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        var query = db.TaskItems
            .Where(t => t.ProjectId == projectId);

        if (statuses != null && statuses.Any())
        {
            var statusIds = statuses.Select(s => (int)s).ToArray();
            query = query.Where(t => statusIds.Contains((int)t.Status));
        }
        
        if (priorityFilter.HasValue) query = query.Where(t => t.Priority == priorityFilter.Value);
        if (myTasksOnly == true) query = query.Where(t => t.AssignedToUserId == currentUserId);
        if (!string.IsNullOrWhiteSpace(taskTypeFilter))
        {
            var normalizedType = taskTypeFilter.Trim().ToLower();
            query = query.Where(t => t.TaskType != null && t.TaskType.ToLower() == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            var normalizedTag = tagFilter.Trim();
            var allMatching = await query
                .Include(t => t.AssignedTo)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var tagFiltered = allMatching
                .Where(t => t.Tags != null && t.Tags.Any(tag => string.Equals(tag, normalizedTag, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var pagedTagItems = tagFiltered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TaskItemSummaryDto
                {
                    Id = t.Id,
                    TaskNumber = t.TaskNumber,
                    TaskHandle = t.TaskHandle,
                    TaskType = t.TaskType,
                    Title = t.Title,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssignedToDisplayName = t.AssignedTo != null ? t.AssignedTo.DisplayName : t.AssignedToName,
                    UpdatedAt = t.UpdatedAt,
                    Tags = t.Tags ?? new List<string>(),
                    SupersededByHandle = t.SupersededByHandle
                })
                .ToList();

            return new PaginatedResponse<TaskItemSummaryDto>
            {
                Items = pagedTagItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = tagFiltered.Count
            };
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskItemSummaryDto
            {
                Id = t.Id,
                TaskNumber = t.TaskNumber,
                TaskHandle = t.TaskHandle,
                TaskType = t.TaskType,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                AssignedToDisplayName = t.AssignedTo != null ? t.AssignedTo.DisplayName : t.AssignedToName,
                UpdatedAt = t.UpdatedAt,
                Tags = t.Tags,
                SupersededByHandle = t.SupersededByHandle
            })
            .ToListAsync();

        return new PaginatedResponse<TaskItemSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TaskItemDto?> GetTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            _logger.LogWarning("User {UserId} attempted to access task {TaskId} without project membership", userId, taskId);
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        return MapToDto(task);
    }

    public async Task<TaskItemDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        if (!await VerifyMembershipAsync(db, projectId, userId))
        {
            _logger.LogWarning("User {UserId} attempted to create task in project {ProjectId} without membership", userId, projectId);
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        // Retry loop for TaskNumber generation
        for (int i = 0; i < 3; i++)
        {
            try
            {
                // Calculate next TaskNumber scoped to ProjectId
                var maxNumber = await db.TaskItems
                    .Where(t => t.ProjectId == projectId)
                    .MaxAsync(t => (int?)t.TaskNumber) ?? 0;

                var nextNumber = maxNumber + 1;

                // Handle TaskHandle logic
                var handle = request.TaskHandle.Trim().ToLowerInvariant();
                if (!handle.StartsWith($"{nextNumber}-"))
                {
                    handle = $"{nextNumber}-{handle}";
                }

                var initialStatus = request.Status
                    ?? (string.Equals(request.TaskType, "Decision", StringComparison.OrdinalIgnoreCase)
                        ? TaskItemStatus.Completed
                        : TaskItemStatus.Open);

                var task = new TaskItem
                {
                    Id = Guid.CreateVersion7(),
                    ProjectId = projectId,
                    Title = request.Title,
                    Body = request.Body,
                    TaskHandle = handle,
                    TaskType = request.TaskType,
                    Priority = request.Priority,
                    Status = initialStatus,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ClosedAt = (initialStatus == TaskItemStatus.Completed || initialStatus == TaskItemStatus.Cancelled)
                        ? DateTime.UtcNow
                        : null,
                    Tags = request.Tags ?? new List<string>(),
                    SupersededByHandle = request.SupersededByHandle,
                    TaskNumber = nextNumber
                };

                db.TaskItems.Add(task);
                await db.SaveChangesAsync();

                await _hubContext.Clients.Group(projectId.ToString()).SendAsync("TaskCreated", task.Id);
                _embeddingQueue.Enqueue(task.Id);

                return MapToDto(task);
            }
            catch (DbUpdateException) when (i < 2)
            {
                // Likely a uniqueness violation on (ProjectId, TaskNumber) due to concurrency.
                // Clear change tracker and retry.
                db.ChangeTracker.Clear();
                _logger.LogWarning("Concurrency conflict generating TaskNumber for project {ProjectId}. Retrying...", projectId);
                await Task.Delay(50 * (i + 1)); // Backoff
            }
        }

        throw new Exception("Failed to create task after multiple attempts due to concurrency.");
    }

    public async Task<TaskItemDto> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) throw new Exception("Task not found");

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        task.Title = request.Title;
        task.Body = request.Body;
        task.Priority = request.Priority;
        task.TaskHandle = request.TaskHandle;
        task.TaskType = request.TaskType;
        if (request.Tags != null)
        {
            task.Tags = request.Tags;
        }
        task.SupersededByHandle = request.SupersededByHandle;
        
        // Handle assignment logic
        if (request.AssignedToUserId != task.AssignedToUserId)
        {
            task.AssignedToUserId = request.AssignedToUserId;
        }
        
        if (request.AssignedToName != task.AssignedToName)
        {
            task.AssignedToName = request.AssignedToName;
        }

        if (task.Status != request.Status)
        {
            task.Status = request.Status;
            if (task.Status == TaskItemStatus.Completed || task.Status == TaskItemStatus.Cancelled)
            {
                task.ClosedAt = DateTime.UtcNow;
            }
            else
            {
                task.ClosedAt = null;
            }
        }

        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskUpdated", task.Id);
        _embeddingQueue.Enqueue(task.Id);

        return MapToDto(task);
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, TaskItemStatus status, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
        if (task == null) throw new Exception("Task not found");

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        if (task.Status != status)
        {
            task.Status = status;
            if (task.Status == TaskItemStatus.Completed || task.Status == TaskItemStatus.Cancelled)
            {
                task.ClosedAt = DateTime.UtcNow;
            }
            else
            {
                task.ClosedAt = null;
            }
            
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskUpdated", task.Id);
            _embeddingQueue.Enqueue(task.Id);
        }
    }

    public async Task<TaskItemDto?> GetTaskByHandleAsync(Guid projectId, string handle, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        if (!await VerifyMembershipAsync(db, projectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskHandle == handle);

        if (task == null) return null;

        return MapToDto(task);
    }

    public async Task<TaskItemDto> ClaimTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) throw new Exception("Task not found");

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        if (task.AssignedToUserId != null && task.AssignedToUserId != userId)
        {
            throw new Exception("Task already assigned");
        }

        task.AssignedToUserId = userId;
        if (task.Status == TaskItemStatus.Open)
        {
            task.Status = TaskItemStatus.InProgress;
        }
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (task.AssignedTo == null)
        {
            task.AssignedTo = await db.Users.FindAsync(userId);
        }

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskClaimed", task.Id, userId);
        return MapToDto(task);
    }

    public async Task<TaskItemDto> ReleaseTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) throw new Exception("Task not found");

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        // Logic fix: Only assigned user or admin can release.
        // We need to fetch membership role.
        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId);
        if (member == null) throw new UnauthorizedAccessException("User is not a member");

        if (task.AssignedToUserId != userId && member.Role != ProjectRole.Admin)
        {
             throw new UnauthorizedAccessException("Only the assignee or an admin can release this task.");
        }

        task.AssignedToUserId = null;
        task.AssignedTo = null;
        if (task.Status == TaskItemStatus.InProgress)
        {
            task.Status = TaskItemStatus.Open;
        }
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskReleased", task.Id);
        return MapToDto(task);
    }

    public async Task<TaskNoteDto> AddNoteAsync(Guid taskId, string content, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        // Need to fetch task first to check project ID
        var task = await db.TaskItems.FindAsync(taskId);
        if (task == null) throw new Exception("Task not found");

        if (!await VerifyMembershipAsync(db, task.ProjectId, userId))
        {
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        var note = new TaskNote
        {
            Id = Guid.CreateVersion7(),
            TaskId = taskId,
            AuthorUserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        db.TaskNotes.Add(note);
        task.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();

        // Fetch author name for DTO
        var author = await db.Users.FindAsync(userId);

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("NoteAdded", taskId);
        _embeddingQueue.Enqueue(taskId);

        return new TaskNoteDto
        {
            Id = note.Id,
            AuthorDisplayName = author?.DisplayName ?? "Unknown",
            Content = note.Content,
            CreatedAt = note.CreatedAt
        };
    }

    public async Task<List<TaskSearchResultDto>> SearchTasksAsync(
        Guid projectId,
        string query,
        TaskItemStatus? statusFilter,
        int top,
        float minScore,
        Guid userId,
        string? taskTypeFilter = null,
        string? tagFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<TaskSearchResultDto>();
        }

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await VerifyMembershipAsync(db, projectId, userId))
        {
            _logger.LogWarning("User {UserId} attempted to search tasks in project {ProjectId} without membership", userId, projectId);
            throw new UnauthorizedAccessException("User is not a member of this project");
        }

        var queryVector = await _embeddingService.GenerateEmbeddingVectorAsync(query, cancellationToken);

        var tasksQuery = db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.Notes)
            .Where(t => t.ProjectId == projectId);

        if (statusFilter.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.Status == statusFilter.Value);
        }
        if (!string.IsNullOrWhiteSpace(taskTypeFilter))
        {
            var normalizedType = taskTypeFilter.Trim().ToLower();
            tasksQuery = tasksQuery.Where(t => t.TaskType != null && t.TaskType.ToLower() == normalizedType);
        }

        var candidateTasks = await tasksQuery.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            var normalizedTag = tagFilter.Trim();
            candidateTasks = candidateTasks
                .Where(t => t.Tags != null && t.Tags.Any(tag => string.Equals(tag, normalizedTag, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var querySpan = queryVector.AsSpan();
        var results = new List<TaskSearchResultDto>();

        foreach (var task in candidateTasks)
        {
            float score = 0f;

            if (task.Embedding != null && task.Embedding.Length == 384 * sizeof(float))
            {
                var taskSpan = _embeddingService.DeserializeVector(task.Embedding);
                score = TensorPrimitives.CosineSimilarity(querySpan, taskSpan);
            }
            else
            {
                var containsInTitle = task.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
                var containsInBody = !string.IsNullOrEmpty(task.Body) && task.Body.Contains(query, StringComparison.OrdinalIgnoreCase);
                var containsInNotes = task.Notes != null && task.Notes.Any(n => !string.IsNullOrEmpty(n.Content) && n.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
                var containsInTags = task.Tags != null && task.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));

                if (containsInTitle || containsInBody || containsInNotes || containsInTags)
                {
                    score = 0.5f;
                }
            }

            if (score >= minScore)
            {
                string preview = BuildMatchedPreview(task, query);

                results.Add(new TaskSearchResultDto
                {
                    Task = new TaskItemSummaryDto
                    {
                        Id = task.Id,
                        TaskNumber = task.TaskNumber,
                        TaskHandle = task.TaskHandle,
                        TaskType = task.TaskType,
                        Title = task.Title,
                        Status = task.Status,
                        Priority = task.Priority,
                        AssignedToDisplayName = task.AssignedTo?.DisplayName ?? task.AssignedToName,
                        UpdatedAt = task.UpdatedAt,
                        Tags = task.Tags ?? new List<string>(),
                        SupersededByHandle = task.SupersededByHandle
                    },
                    SimilarityScore = score,
                    MatchedPreview = preview
                });
            }
        }

        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(top)
            .ToList();
    }

    private static string BuildMatchedPreview(TaskItem task, string query)
    {
        var prefix = !string.IsNullOrWhiteSpace(task.SupersededByHandle)
            ? $"[Superseded by {task.SupersededByHandle}] "
            : string.Empty;

        var queryTerms = query
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int CountMatches(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || queryTerms.Length == 0) return 0;
            int hits = 0;
            foreach (var term in queryTerms)
            {
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase)) hits++;
            }
            return hits;
        }

        int bodyHits = CountMatches(task.Body);
        TaskNote? bestNote = null;
        int bestNoteHits = 0;

        if (task.Notes != null && task.Notes.Count > 0)
        {
            foreach (var note in task.Notes.OrderByDescending(n => n.CreatedAt))
            {
                int hits = CountMatches(note.Content);
                if (hits > bestNoteHits)
                {
                    bestNoteHits = hits;
                    bestNote = note;
                }
            }
        }

        string rawSnippet;
        if (bestNote != null && (bestNoteHits > bodyHits || string.IsNullOrWhiteSpace(task.Body)))
        {
            rawSnippet = $"[Note] {TruncateSnippet(bestNote.Content, queryTerms, 160)}";
        }
        else if (!string.IsNullOrWhiteSpace(task.Body))
        {
            rawSnippet = TruncateSnippet(task.Body, queryTerms, 160);
        }
        else if (task.Notes != null && task.Notes.Count > 0)
        {
            var latestNote = task.Notes.OrderByDescending(n => n.CreatedAt).First();
            rawSnippet = $"[Note] {TruncateSnippet(latestNote.Content, queryTerms, 160)}";
        }
        else
        {
            rawSnippet = task.Title;
        }

        return prefix + rawSnippet;
    }

    private static string TruncateSnippet(string text, string[] queryTerms, int maxLength)
    {
        var normalized = text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength) return normalized;

        foreach (var term in queryTerms)
        {
            int idx = normalized.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int start = Math.Max(0, idx - 40);
                int length = Math.Min(maxLength, normalized.Length - start);
                var slice = normalized.Substring(start, length);
                return (start > 0 ? "..." : "") + slice + (start + length < normalized.Length ? "..." : "");
            }
        }

        return normalized.Substring(0, maxLength) + "...";
    }

    private async Task<bool> VerifyMembershipAsync(WatrDbContext db, Guid projectId, Guid userId)
    {
        return await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
    }

    private static TaskItemDto MapToDto(TaskItem t)
    {
        return new TaskItemDto
        {
            Id = t.Id,
            ProjectId = t.ProjectId,
            TaskNumber = t.TaskNumber,
            TaskHandle = t.TaskHandle,
            TaskType = t.TaskType,
            Title = t.Title,
            Body = t.Body,
            Status = t.Status,
            Priority = t.Priority,
            AssignedToUserId = t.AssignedToUserId,
            AssignedToName = t.AssignedToName,
            AssignedToDisplayName = t.AssignedTo?.DisplayName ?? t.AssignedToName,
            CreatedByUserId = t.CreatedByUserId,
            CreatedByDisplayName = t.CreatedBy?.DisplayName ?? "Unknown",
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            ClosedAt = t.ClosedAt,
            Tags = t.Tags ?? new List<string>(),
            SupersededByHandle = t.SupersededByHandle,
            Notes = t.Notes.Select(n => new TaskNoteDto
            {
                Id = n.Id,
                AuthorDisplayName = n.Author?.DisplayName ?? "Unknown",
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            }).ToList()
        };
    }
}
