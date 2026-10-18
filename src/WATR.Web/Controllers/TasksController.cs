using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using WATR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WATR.Web.Controllers;

public class TasksController : ApiControllerBase
{
    private readonly TaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(TaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpGet("/api/v1/projects/{projectId}/tasks")]
    public async Task<ActionResult<PaginatedResponse<TaskItemSummaryDto>>> GetProjectTasks(
        Guid projectId,
        [FromQuery] TaskItemStatus? status,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? assignedTo,
        [FromQuery] string? taskType = null,
        [FromQuery] string? tag = null,
        int page = 1,
        int pageSize = 20)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var statuses = status.HasValue ? new[] { status.Value } : null;
        var myTasksOnly = assignedTo == "me";

        try
        {
            var result = await _taskService.GetTasksAsync(projectId, statuses, priority, myTasksOnly, userId, page, pageSize, taskType, tag);
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("/api/v1/projects/{projectId}/tasks/search")]
    public async Task<ActionResult<List<TaskSearchResultDto>>> SearchProjectTasks(
        Guid projectId,
        [FromQuery] string q,
        [FromQuery] TaskItemStatus? status = null,
        [FromQuery] string? taskType = null,
        [FromQuery] string? tag = null,
        [FromQuery] int top = 10,
        [FromQuery] float minScore = 0.25f)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var results = await _taskService.SearchTasksAsync(projectId, q, status, top, minScore, userId, taskType, tag);
            return results;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tasks for project {ProjectId}", projectId);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("/api/v1/projects/{projectId}/tasks")]
    public async Task<ActionResult<TaskItemDto>> CreateTask(Guid projectId, CreateTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try 
        {
            var task = await _taskService.CreateTaskAsync(projectId, request, userId);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("/api/v1/projects/{projectId}/tasks/handle/{handle}")]
    public async Task<ActionResult<TaskItemDto>> GetTaskByHandle(Guid projectId, string handle)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var task = await _taskService.GetTaskByHandleAsync(projectId, handle, userId);
            if (task == null) return NotFound();
            return task;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItemDto>> GetTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var task = await _taskService.GetTaskAsync(id, userId);
            if (task == null) return NotFound();
            return task;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TaskItemDto>> PatchTask(Guid id, PatchTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var task = await _taskService.GetTaskAsync(id, userId);
            if (task == null) return NotFound();

            var updateRequest = new UpdateTaskRequest
            {
                Title = request.Title ?? task.Title,
                Body = request.Body ?? task.Body,
                TaskHandle = request.TaskHandle ?? task.TaskHandle,
                TaskType = request.TaskType ?? task.TaskType,
                Status = request.Status ?? task.Status,
                Priority = request.Priority ?? task.Priority,
                AssignedToUserId = request.AssignedToUserId ?? task.AssignedToUserId,
                AssignedToName = request.AssignedToName ?? task.AssignedToName,
                Tags = request.Tags ?? task.Tags,
                SupersededByHandle = request.SupersededByHandle ?? task.SupersededByHandle
            };

            var updated = await _taskService.UpdateTaskAsync(id, updateRequest, userId);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/claim")]
    public async Task<ActionResult<TaskItemDto>> ClaimTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var updated = await _taskService.ClaimTaskAsync(id, userId);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("already assigned")) return Conflict(new { error = "already_assigned", message = ex.Message });
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/release")]
    public async Task<ActionResult<TaskItemDto>> ReleaseTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var updated = await _taskService.ReleaseTaskAsync(id, userId);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/notes")]
    public async Task<ActionResult<TaskNoteDto>> AddNote(Guid id, CreateTaskNoteRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var note = await _taskService.AddNoteAsync(id, request.Content, userId);
            return CreatedAtAction(nameof(GetTask), new { id = id }, note);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
