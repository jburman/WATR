using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using HotShallot.Web.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotShallot.Web.Controllers;

public class TasksController : ApiControllerBase
{
    private readonly HotShallotDbContext _db;
    private readonly IHubContext<TaskHub> _hubContext;

    public TasksController(HotShallotDbContext db, IHubContext<TaskHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    [HttpGet("/api/v1/projects/{projectId}/tasks")]
    public async Task<ActionResult<PaginatedResponse<TaskItemSummaryDto>>> GetProjectTasks(
        Guid projectId,
        [FromQuery] TaskItemStatus? status,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? assignedTo,
        int page = 1,
        int pageSize = 20)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        // Verify membership
        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId))
        {
            return Forbid();
        }

        var query = _db.TaskItems
            .Where(t => t.ProjectId == projectId);

        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);
        if (assignedTo == "me") query = query.Where(t => t.AssignedToUserId == userId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt) // UUID v7 is sortable by time
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

    [HttpPost("/api/v1/projects/{projectId}/tasks")]
    public async Task<ActionResult<TaskItemDto>> CreateTask(Guid projectId, CreateTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId))
        {
            return Forbid();
        }

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

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group(projectId.ToString()).SendAsync("TaskCreated", task.Id);

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, MapToDto(task));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItemDto>> GetTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var task = await _db.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();

        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId))
        {
            return Forbid();
        }

        return MapToDto(task);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> PatchTask(Guid id, PatchTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var task = await _db.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId))
        {
            return Forbid();
        }

        if (request.Title != null) task.Title = request.Title;
        if (request.Body != null) task.Body = request.Body;
        if (request.Priority.HasValue) task.Priority = request.Priority.Value;
        
        if (request.Status.HasValue)
        {
            if (request.Status.Value != task.Status)
            {
                task.Status = request.Status.Value;
                if (task.Status == TaskItemStatus.Completed || task.Status == TaskItemStatus.Cancelled)
                {
                    task.ClosedAt = DateTime.UtcNow;
                }
                else
                {
                    task.ClosedAt = null;
                }
            }
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskUpdated", task.Id);

        return NoContent();
    }

    [HttpPost("{id}/claim")]
    public async Task<IActionResult> ClaimTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var task = await _db.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId))
        {
            return Forbid();
        }

        if (task.AssignedToUserId != null && task.AssignedToUserId != userId)
        {
            return Conflict(new { error = "already_assigned", message = "Task is already assigned to someone else." });
        }

        task.AssignedToUserId = userId;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskClaimed", task.Id, userId);

        return NoContent();
    }

    [HttpPost("{id}/release")]
    public async Task<IActionResult> ReleaseTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var task = await _db.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        var member = await _db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId);
        if (member == null) return Forbid();

        if (task.AssignedToUserId != userId && member.Role != ProjectRole.Admin)
        {
            return Forbid();
        }

        task.AssignedToUserId = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("TaskReleased", task.Id);

        return NoContent();
    }

    [HttpPost("{id}/notes")]
    public async Task<ActionResult<TaskNoteDto>> AddNote(Guid id, CreateTaskNoteRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var task = await _db.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        if (!await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId))
        {
            return Forbid();
        }

        var note = new TaskNote
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            AuthorUserId = userId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskNotes.Add(note);
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Fetch author name
        var author = await _db.Users.FindAsync(userId);

        await _hubContext.Clients.Group(task.ProjectId.ToString()).SendAsync("NoteAdded", task.Id);

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, new TaskNoteDto
        {
            Id = note.Id,
            AuthorDisplayName = author?.DisplayName ?? "Unknown",
            Content = note.Content,
            CreatedAt = note.CreatedAt
        });
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
