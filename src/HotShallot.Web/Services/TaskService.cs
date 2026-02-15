using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using HotShallot.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HotShallot.Web.Services;

public class TaskService
{
    private readonly IDbContextFactory<HotShallotDbContext> _dbFactory;
    private readonly IHubContext<TaskHub> _hubContext;

    public TaskService(IDbContextFactory<HotShallotDbContext> dbFactory, IHubContext<TaskHub> hubContext)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
    }

    public async Task<PaginatedResponse<TaskItemSummaryDto>> GetTasksAsync(Guid projectId, IEnumerable<TaskItemStatus>? statuses, TaskPriority? priorityFilter, bool? myTasksOnly, Guid currentUserId, int page, int pageSize)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
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
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                AssignedToDisplayName = t.AssignedTo != null ? t.AssignedTo.DisplayName : null,
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

    public async Task<TaskItemDto?> GetTaskAsync(Guid taskId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        return MapToDto(task);
    }

    public async Task<TaskItemDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Title = request.Title,
            Body = request.Body,
            Priority = request.Priority,
            Status = TaskItemStatus.Open,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(projectId.ToString()).SendAsync("TaskCreated", task.Id);

        return MapToDto(task);
    }

    public async Task UpdateTaskAsync(Guid taskId, UpdateTaskRequest request)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
        if (task == null) throw new Exception("Task not found");

        task.Title = request.Title;
        task.Body = request.Body;
        task.Priority = request.Priority;
        
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

    public async Task ClaimTaskAsync(Guid taskId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
        if (task == null) throw new Exception("Task not found");

        if (task.AssignedToUserId != null && task.AssignedToUserId != userId)
        {
            throw new Exception("Task already assigned");
        }

        task.AssignedToUserId = userId;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskClaimed", task.Id, userId);
    }

    public async Task ReleaseTaskAsync(Guid taskId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.TaskItems.FindAsync(taskId);
        if (task == null) throw new Exception("Task not found");

        task.AssignedToUserId = null;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskReleased", task.Id);
    }

    public async Task AddNoteAsync(Guid taskId, string content, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var note = new TaskNote
        {
            Id = Guid.CreateVersion7(),
            TaskId = taskId,
            AuthorUserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        db.TaskNotes.Add(note);
        var task = await db.TaskItems.FindAsync(taskId);
        if (task != null) task.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(task?.ProjectId.ToString() ?? "").SendAsync("NoteAdded", taskId);
    }

    private static TaskItemDto MapToDto(TaskItem t)
    {
        return new TaskItemDto
        {
            Id = t.Id,
            ProjectId = t.ProjectId,
            Title = t.Title,
            Body = t.Body,
            Status = t.Status,
            Priority = t.Priority,
            AssignedToUserId = t.AssignedToUserId,
            AssignedToDisplayName = t.AssignedTo?.DisplayName,
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
