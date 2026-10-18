using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;

namespace WATR.Cli.Mcp;

[McpServerToolType]
public class WatrMcpTools
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;
    private readonly ProjectContextHelper _projectContext;

    public WatrMcpTools(ApiClient api, ConfigService config, ProjectContextHelper projectContext)
    {
        _api = api;
        _config = config;
        _projectContext = projectContext;
    }

    [McpServerTool(Name = "watr_status", ReadOnly = true)]
    [Description("Get current WATR status including server URL, authenticated user, and active project context.")]
    public async Task<WatrStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _api.GetAsync<UserDto>("api/v1/user/me");
            var config = _config.Load();
            var localSettings = _config.GetLocalSettings();
            var resolvedProject = _projectContext.ResolveProjectName(null);

            return new WatrStatusDto
            {
                ServerUrl = config.ServerUrl,
                UserDisplayName = user.DisplayName,
                UserEmail = user.Email,
                LocalProject = localSettings?.ProjectName,
                DefaultProject = config.DefaultProject,
                ActiveProject = resolvedProject
            };
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to get WATR status: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_list_projects", ReadOnly = true)]
    [Description("List all projects accessible to the authenticated user.")]
    public async Task<List<ProjectDto>> ListProjectsAsync(
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var path = $"api/v1/projects?page={page}&pageSize={pageSize}";
            var response = await _api.GetAsync<PaginatedResponse<ProjectDto>>(path);
            return response.Items;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to list projects: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_create_project", ReadOnly = false, Destructive = false)]
    [Description("Create a new project in WATR and link it to the current workspace.")]
    public async Task<ProjectDto> CreateProjectAsync(
        [Description("Unique project name")] string name,
        [Description("Optional project description")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new McpException("Project name is required.");
        }

        try
        {
            var request = new CreateProjectRequest
            {
                Name = name.Trim(),
                Description = description ?? string.Empty
            };
            var created = await _api.PostAsync<ProjectDto>("api/v1/projects", request);

            var localSettings = _config.GetLocalSettings() ?? new LocalProjectSettings();
            localSettings.ProjectName = created.Name;
            _config.SaveLocalSettings(localSettings);

            return created;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to create project '{name}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_set_project", ReadOnly = false, Destructive = false)]
    [Description("Set or link the active project context for the current workspace directory (.watr/settings.json).")]
    public async Task<string> SetProjectAsync(
        [Description("Project name to bind to current workspace")] string project,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            throw new McpException("Project name is required.");
        }

        try
        {
            var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
            var target = projects.Items.FirstOrDefault(p => p.Name.Equals(project, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                throw new McpException($"Project '{project}' was not found on server.");
            }

            var localSettings = _config.GetLocalSettings() ?? new LocalProjectSettings();
            localSettings.ProjectName = target.Name;
            _config.SaveLocalSettings(localSettings);

            return $"Workspace successfully linked to project '{target.Name}'.";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to set active project: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_list_tasks", ReadOnly = true)]
    [Description("List tasks or decision records within a project with optional filtering by status, priority, taskType, tag, and assignment.")]
    public async Task<List<TaskItemSummaryDto>> ListTasksAsync(
        [Description("Project name (defaults to active project if omitted)")] string? project = null,
        [Description("Filter by task status: Open, InProgress, Completed, Blocked, Cancelled")] string? status = null,
        [Description("Filter by priority: None, Low, Medium, High, Critical")] string? priority = null,
        [Description("Filter to tasks assigned to me ('me') or null for all")] string? assignedTo = null,
        [Description("Filter by task type (e.g. 'Task', 'Epic', 'Bug', 'Decision')")] string? taskType = null,
        [Description("Filter by tag or component (e.g. 'auth', 'OnnxEmbeddingService.cs')")] string? tag = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDto = await ResolveProjectDtoAsync(project, cancellationToken);
            var path = $"api/v1/projects/{projectDto.Id}/tasks?page={page}&pageSize={pageSize}";

            var parsedStatus = ParseStatus(status);
            if (parsedStatus.HasValue)
            {
                path += $"&status={parsedStatus.Value}";
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                path += $"&assignedTo={Uri.EscapeDataString(assignedTo)}";
            }
            if (!string.IsNullOrWhiteSpace(taskType))
            {
                path += $"&taskType={Uri.EscapeDataString(taskType)}";
            }
            if (!string.IsNullOrWhiteSpace(tag))
            {
                path += $"&tag={Uri.EscapeDataString(tag)}";
            }

            var response = await _api.GetAsync<PaginatedResponse<TaskItemSummaryDto>>(path);
            var items = response.Items;

            if (!string.IsNullOrWhiteSpace(priority))
            {
                var parsedPriority = ParsePriority(priority);
                items = items.Where(t => t.Priority == parsedPriority).ToList();
            }

            return items;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to list tasks: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_search_tasks", ReadOnly = true)]
    [Description("Perform semantic vector search across project tasks, notes, and architectural decisions by natural language query/intent.")]
    public async Task<List<TaskSearchResultDto>> SearchTasksAsync(
        [Description("Natural language search query (e.g. 'implement auth tokens', 'fix database deadlock')")] string query,
        [Description("Project name (defaults to active project if omitted)")] string? project = null,
        [Description("Filter by task status: Open, InProgress, Completed, Blocked, Cancelled")] string? status = null,
        [Description("Filter by task type (e.g. 'Task', 'Epic', 'Bug', 'Decision')")] string? taskType = null,
        [Description("Filter by tag or component (e.g. 'auth', 'OnnxEmbeddingService.cs')")] string? tag = null,
        [Description("Maximum number of results to return (default: 10)")] int top = 10,
        [Description("Minimum cosine similarity score threshold 0.0 to 1.0 (default: 0.25)")] float minScore = 0.25f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("Query cannot be empty.");
        }

        try
        {
            var projectDto = await ResolveProjectDtoAsync(project, cancellationToken);
            var path = $"api/v1/projects/{projectDto.Id}/tasks/search?q={Uri.EscapeDataString(query)}&top={top}&minScore={minScore}";

            var parsedStatus = ParseStatus(status);
            if (parsedStatus.HasValue)
            {
                path += $"&status={parsedStatus.Value}";
            }
            if (!string.IsNullOrWhiteSpace(taskType))
            {
                path += $"&taskType={Uri.EscapeDataString(taskType)}";
            }
            if (!string.IsNullOrWhiteSpace(tag))
            {
                path += $"&tag={Uri.EscapeDataString(tag)}";
            }

            return await _api.GetAsync<List<TaskSearchResultDto>>(path);
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to search tasks: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_get_task", ReadOnly = true)]
    [Description("Get full details of a task by ID (GUID) or task handle (e.g. 'WATR-1').")]
    public async Task<TaskItemDto> GetTaskAsync(
        [Description("Task ID (GUID) or Task Handle (e.g. 'WATR-1')")] string idOrHandle,
        [Description("Project name (optional, used if resolving by task handle when not a GUID)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrHandle))
        {
            throw new McpException("Task ID or handle is required.");
        }

        try
        {
            if (Guid.TryParse(idOrHandle, out var id))
            {
                return await _api.GetAsync<TaskItemDto>($"api/v1/tasks/{id}");
            }

            var projectDto = await ResolveProjectDtoAsync(project, cancellationToken);
            return await _api.GetAsync<TaskItemDto>($"api/v1/projects/{projectDto.Id}/tasks/handle/{Uri.EscapeDataString(idOrHandle)}");
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Task '{idOrHandle}' not found or request failed: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_create_task", ReadOnly = false, Destructive = false)]
    [Description("Create a new task, epic, bug, or decision record in a project.")]
    public async Task<TaskItemDto> CreateTaskAsync(
        [Description("Task title")] string title,
        [Description("Unique task handle (e.g. 'PROJ-1', 'AUTH-42')")] string handle,
        [Description("Detailed task description or body content")] string? body = null,
        [Description("Task priority: None, Low, Medium, High, Critical")] string? priority = null,
        [Description("Optional task category or type (e.g. 'Task', 'Epic', 'Bug', 'Decision')")] string? taskType = null,
        [Description("Optional initial status: Open, InProgress, Completed, Blocked, Cancelled")] string? status = null,
        [Description("Optional comma-separated tags or related file paths (e.g. 'auth,OnnxEmbeddingService.cs')")] string? tags = null,
        [Description("Optional handle of a newer task/decision that supersedes this item")] string? supersededByHandle = null,
        [Description("Project name (defaults to active project if omitted)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new McpException("Task title is required.");
        if (string.IsNullOrWhiteSpace(handle)) throw new McpException("Task handle is required.");

        try
        {
            var projectDto = await ResolveProjectDtoAsync(project, cancellationToken);

            var resolvedBody = body ?? string.Empty;
            if (resolvedBody.StartsWith("@") && File.Exists(resolvedBody[1..]))
            {
                resolvedBody = await File.ReadAllTextAsync(resolvedBody[1..], cancellationToken);
            }

            var request = new CreateTaskRequest
            {
                Title = title.Trim(),
                TaskHandle = handle.Trim(),
                Body = resolvedBody,
                Priority = ParsePriority(priority),
                TaskType = taskType,
                Status = ParseStatus(status),
                Tags = ParseTags(tags),
                SupersededByHandle = string.IsNullOrWhiteSpace(supersededByHandle) ? null : supersededByHandle.Trim()
            };

            return await _api.PostAsync<TaskItemDto>($"api/v1/projects/{projectDto.Id}/tasks", request);
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to create task '{handle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_record_decision", ReadOnly = false, Destructive = false)]
    [Description("Record an architectural decision, trade-off rationale ('why X was chosen over Y'), or investigation summary as a Completed 'Decision' record for future semantic lookup.")]
    public async Task<TaskItemDto> RecordDecisionAsync(
        [Description("Concise decision title (e.g. 'Use SQLite BLOB + SIMD for vector search')")] string title,
        [Description("Short kebab-case decision handle slug (e.g. 'adr-sqlite-vectors')")] string handle,
        [Description("Markdown body explaining context, options considered, decision rationale, and consequences")] string body,
        [Description("Optional comma-separated tags or affected file/component paths (e.g. 'search,sqlite,OnnxEmbeddingService.cs')")] string? tags = null,
        [Description("Optional handle or GUID of an older decision that this decision supersedes")] string? supersedesHandle = null,
        [Description("Project name (defaults to active project if omitted)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new McpException("Decision title is required.");
        if (string.IsNullOrWhiteSpace(handle)) throw new McpException("Decision handle is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new McpException("Decision body/rationale is required.");

        try
        {
            var projectDto = await ResolveProjectDtoAsync(project, cancellationToken);

            var resolvedBody = body;
            if (resolvedBody.StartsWith("@") && File.Exists(resolvedBody[1..]))
            {
                resolvedBody = await File.ReadAllTextAsync(resolvedBody[1..], cancellationToken);
            }

            var createRequest = new CreateTaskRequest
            {
                Title = title.Trim(),
                TaskHandle = handle.Trim(),
                Body = resolvedBody,
                TaskType = "Decision",
                Priority = TaskPriority.Medium,
                Status = TaskItemStatus.Completed,
                Tags = ParseTags(tags)
            };

            var created = await _api.PostAsync<TaskItemDto>($"api/v1/projects/{projectDto.Id}/tasks", createRequest);

            if (!string.IsNullOrWhiteSpace(supersedesHandle) && !string.IsNullOrWhiteSpace(created.TaskHandle))
            {
                var oldId = await ResolveTaskIdAsync(supersedesHandle.Trim(), projectDto.Name, cancellationToken);
                await _api.PatchAsync<TaskItemDto>($"api/v1/tasks/{oldId}", new PatchTaskRequest
                {
                    SupersededByHandle = created.TaskHandle
                });
            }

            return created;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to record decision '{handle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_update_task", ReadOnly = false, Destructive = false)]
    [Description("Update fields of an existing task.")]
    public async Task<TaskItemDto> UpdateTaskAsync(
        [Description("Task ID (GUID) or task handle")] string idOrHandle,
        [Description("New title")] string? title = null,
        [Description("New body / description")] string? body = null,
        [Description("New status: Open, InProgress, Completed, Blocked, Cancelled")] string? status = null,
        [Description("New priority: None, Low, Medium, High, Critical")] string? priority = null,
        [Description("New task handle")] string? handle = null,
        [Description("New task type")] string? taskType = null,
        [Description("Optional comma-separated tags or related file paths")] string? tags = null,
        [Description("Optional handle of a newer task/decision that supersedes this item")] string? supersededByHandle = null,
        [Description("Project name (needed if idOrHandle is a handle)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var taskId = await ResolveTaskIdAsync(idOrHandle, project, cancellationToken);

            var resolvedBody = body;
            if (resolvedBody != null && resolvedBody.StartsWith("@") && File.Exists(resolvedBody[1..]))
            {
                resolvedBody = await File.ReadAllTextAsync(resolvedBody[1..], cancellationToken);
            }

            var patchRequest = new PatchTaskRequest
            {
                Title = title,
                Body = resolvedBody,
                Status = ParseStatus(status),
                Priority = string.IsNullOrWhiteSpace(priority) ? null : ParsePriority(priority),
                TaskHandle = handle,
                TaskType = taskType,
                Tags = ParseTags(tags),
                SupersededByHandle = supersededByHandle
            };

            return await _api.PatchAsync<TaskItemDto>($"api/v1/tasks/{taskId}", patchRequest);
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to update task '{idOrHandle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_claim_task", ReadOnly = false, Destructive = false)]
    [Description("Claim a task for the authenticated user, setting its status to InProgress.")]
    public async Task<TaskItemDto> ClaimTaskAsync(
        [Description("Task ID (GUID) or task handle")] string idOrHandle,
        [Description("Project name (needed if idOrHandle is a handle)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var taskId = await ResolveTaskIdAsync(idOrHandle, project, cancellationToken);
            return await _api.PostAsync<TaskItemDto>($"api/v1/tasks/{taskId}/claim");
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to claim task '{idOrHandle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_release_task", ReadOnly = false, Destructive = false)]
    [Description("Release a task previously claimed by the current user, reverting it to unassigned Open status.")]
    public async Task<TaskItemDto> ReleaseTaskAsync(
        [Description("Task ID (GUID) or task handle")] string idOrHandle,
        [Description("Project name (needed if idOrHandle is a handle)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var taskId = await ResolveTaskIdAsync(idOrHandle, project, cancellationToken);
            return await _api.PostAsync<TaskItemDto>($"api/v1/tasks/{taskId}/release");
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to release task '{idOrHandle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_complete_task", ReadOnly = false, Destructive = false)]
    [Description("Mark a task as completed, optionally appending a final verification note.")]
    public async Task<TaskItemDto> CompleteTaskAsync(
        [Description("Task ID (GUID) or task handle")] string idOrHandle,
        [Description("Optional verification or completion note to append before marking Completed")] string? verificationNote = null,
        [Description("Project name (needed if idOrHandle is a handle)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var taskId = await ResolveTaskIdAsync(idOrHandle, project, cancellationToken);
            if (!string.IsNullOrWhiteSpace(verificationNote))
            {
                await _api.PostAsync<TaskNoteDto>($"api/v1/tasks/{taskId}/notes", new CreateTaskNoteRequest { Content = verificationNote.Trim() });
            }
            return await _api.PatchAsync<TaskItemDto>($"api/v1/tasks/{taskId}", new PatchTaskRequest { Status = TaskItemStatus.Completed });
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to complete task '{idOrHandle}': {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "watr_add_note", ReadOnly = false, Destructive = false)]
    [Description("Add a progress note or comment to a task.")]
    public async Task<TaskNoteDto> AddNoteAsync(
        [Description("Task ID (GUID) or task handle")] string idOrHandle,
        [Description("Content of the note")] string content,
        [Description("Project name (needed if idOrHandle is a handle)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new McpException("Note content is required.");
        }

        try
        {
            var taskId = await ResolveTaskIdAsync(idOrHandle, project, cancellationToken);

            var resolvedContent = content;
            if (resolvedContent.StartsWith("@") && File.Exists(resolvedContent[1..]))
            {
                resolvedContent = await File.ReadAllTextAsync(resolvedContent[1..], cancellationToken);
            }

            return await _api.PostAsync<TaskNoteDto>($"api/v1/tasks/{taskId}/notes", new CreateTaskNoteRequest { Content = resolvedContent });
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to add note to task '{idOrHandle}': {ex.Message}", ex);
        }
    }

    private async Task<ProjectDto> ResolveProjectDtoAsync(string? explicitProjectName, CancellationToken cancellationToken)
    {
        var projectName = await _projectContext.ResolveProjectAsync(explicitProjectName, cancellationToken);
        var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
        var project = projects.Items.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
        if (project == null)
        {
            throw new McpException($"Project '{projectName}' was not found on server.");
        }
        return project;
    }

    private async Task<Guid> ResolveTaskIdAsync(string idOrHandle, string? explicitProjectName, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrHandle, out var id))
        {
            return id;
        }

        var project = await ResolveProjectDtoAsync(explicitProjectName, cancellationToken);
        try
        {
            var task = await _api.GetAsync<TaskItemDto>($"api/v1/projects/{project.Id}/tasks/handle/{Uri.EscapeDataString(idOrHandle)}");
            return task.Id;
        }
        catch (Exception ex)
        {
            throw new McpException($"Task with handle '{idOrHandle}' not found in project '{project.Name}': {ex.Message}", ex);
        }
    }

    private static TaskPriority ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return TaskPriority.None;
        if (Enum.TryParse<TaskPriority>(priority, true, out var p)) return p;
        throw new McpException($"Invalid priority '{priority}'. Valid priorities: None, Low, Medium, High, Critical.");
    }

    private static TaskItemStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        if (Enum.TryParse<TaskItemStatus>(status, true, out var s)) return s;
        throw new McpException($"Invalid status '{status}'. Valid statuses: Open, InProgress, Completed, Blocked, Cancelled.");
    }

    private static List<string>? ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return null;
        return tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
