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
            var result = await _taskService.GetTasksAsync(projectId, statuses, priority, myTasksOnly, userId, page, pageSize);
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
        [FromQuery] int top = 10,
        [FromQuery] float minScore = 0.25f)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var results = await _taskService.SearchTasksAsync(projectId, q, status, top, minScore, userId);
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
    public async Task<IActionResult> PatchTask(Guid id, PatchTaskRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var updateRequest = new UpdateTaskRequest
        {
            Title = request.Title ?? "",
            Body = request.Body ?? "",
            TaskHandle = request.TaskHandle,
            Status = request.Status ?? TaskItemStatus.Open,
            Priority = request.Priority ?? TaskPriority.None
        };
        
        try
        {
            var task = await _taskService.GetTaskAsync(id, userId);
            if (task == null) return NotFound();

            updateRequest.Title = request.Title ?? task.Title;
            updateRequest.Body = request.Body ?? task.Body;
            updateRequest.TaskHandle = request.TaskHandle ?? task.TaskHandle;
            updateRequest.Status = request.Status == null ? task.Status : request.Status.Value;
            updateRequest.Priority = request.Priority == null ? task.Priority : request.Priority.Value;

            await _taskService.UpdateTaskAsync(id, updateRequest, userId);
            return NoContent();
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
    public async Task<IActionResult> ClaimTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            await _taskService.ClaimTaskAsync(id, userId);
            return NoContent();
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
    public async Task<IActionResult> ReleaseTask(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        try
        {
            await _taskService.ReleaseTaskAsync(id, userId);
            return NoContent();
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
