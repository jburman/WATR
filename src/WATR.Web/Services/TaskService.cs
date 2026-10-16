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

    public TaskService(IDbContextFactory<WatrDbContext> dbFactory, IHubContext<TaskHub> hubContext, ILogger<TaskService> logger)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<PaginatedResponse<TaskItemSummaryDto>> GetTasksAsync(Guid projectId, IEnumerable<TaskItemStatus>? statuses, TaskPriority? priorityFilter, bool? myTasksOnly, Guid currentUserId, int page, int pageSize)
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
                UpdatedAt = t.UpdatedAt
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

                var task = new TaskItem
                {
                    Id = Guid.CreateVersion7(),
                    ProjectId = projectId,
                    Title = request.Title,
                    Body = request.Body,
                    TaskHandle = handle,
                    TaskType = request.TaskType,
                    Priority = request.Priority,
                    Status = TaskItemStatus.Open,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TaskNumber = nextNumber
                };

                db.TaskItems.Add(task);
                await db.SaveChangesAsync();

                await _hubContext.Clients.Group(projectId.ToString()).SendAsync("TaskCreated", task.Id);

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

    public async Task UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
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

    public async Task ClaimTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
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
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskClaimed", task.Id, userId);
    }

    public async Task ReleaseTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
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
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskReleased", task.Id);
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

        return new TaskNoteDto
        {
            Id = note.Id,
            AuthorDisplayName = author?.DisplayName ?? "Unknown",
            Content = note.Content,
            CreatedAt = note.CreatedAt
        };
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
