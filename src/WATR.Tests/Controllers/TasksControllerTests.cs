using System.Net.Http.Json;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using Xunit;

namespace WATR.Tests.Controllers;

public class TasksControllerTests : ControllerTestBase
{
    public TasksControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateTask_ShouldReturnCreated()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        var request = new CreateTaskRequest
        {
            Title = "New Task",
            TaskHandle = "new-task",
            Body = "This is a test task",
            Priority = TaskPriority.High
        };

        var response = await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", request);
        
        response.EnsureSuccessStatusCode();
        var task = await response.Content.ReadFromJsonAsync<TaskItemDto>();
        
        Assert.NotNull(task);
        Assert.Equal(request.Title, task.Title);
        // Verify handle prefix logic
        Assert.Equal($"{task.TaskNumber}-new-task", task.TaskHandle);
        Assert.Equal(TaskItemStatus.Open, task.Status);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(user.Id, task.CreatedByUserId);
    }

    [Fact]
    public async Task GetTasks_ShouldReturnTasks()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        // Create a task
        var createRequest = new CreateTaskRequest { Title = "Task 1", TaskHandle = "task-1" };
        await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", createRequest);

        var response = await _client.GetAsync($"api/v1/projects/{project.Id}/tasks");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<TaskItemSummaryDto>>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("Task 1", page.Items[0].Title);
    }

    [Fact]
    public async Task PatchTask_ShouldUpdateStatus()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        // Create task
        var createRes = await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", new CreateTaskRequest { Title = "To Update", TaskHandle = "update-me" });
        var task = await createRes.Content.ReadFromJsonAsync<TaskItemDto>();

        // Patch
        var patch = new PatchTaskRequest { Status = TaskItemStatus.InProgress };
        var response = await _client.PatchAsJsonAsync($"api/v1/tasks/{task!.Id}", patch);
        
        Assert.True(response.IsSuccessStatusCode);

        // Verify
        var getRes = await _client.GetAsync($"api/v1/tasks/{task.Id}");
        var updatedTask = await getRes.Content.ReadFromJsonAsync<TaskItemDto>();
        Assert.Equal(TaskItemStatus.InProgress, updatedTask!.Status);
    }

    [Fact]
    public async Task ClaimTask_ShouldAssignUser()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        // Create task
        var createRes = await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", new CreateTaskRequest { Title = "To Claim", TaskHandle = "claim-me" });
        var task = await createRes.Content.ReadFromJsonAsync<TaskItemDto>();

        // Claim
        var response = await _client.PostAsync($"api/v1/tasks/{task!.Id}/claim", null);
        response.EnsureSuccessStatusCode();

        // Verify
        var getRes = await _client.GetAsync($"api/v1/tasks/{task.Id}");
        var updatedTask = await getRes.Content.ReadFromJsonAsync<TaskItemDto>();
        Assert.Equal(user.Id, updatedTask!.AssignedToUserId);
    }

    [Fact]
    public async Task AddNote_ShouldAddNote()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        // Create task
        var createRes = await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", new CreateTaskRequest { Title = "With Note", TaskHandle = "note-me" });
        var task = await createRes.Content.ReadFromJsonAsync<TaskItemDto>();

        // Add note
        var noteReq = new CreateTaskNoteRequest { Content = "This is a note" };
        var response = await _client.PostAsJsonAsync($"api/v1/tasks/{task!.Id}/notes", noteReq);
        response.EnsureSuccessStatusCode();

        // Verify return type
        var note = await response.Content.ReadFromJsonAsync<TaskNoteDto>();
        Assert.NotNull(note);
        Assert.Equal("This is a note", note.Content);

        // Verify in task
        var getRes = await _client.GetAsync($"api/v1/tasks/{task.Id}");
        var updatedTask = await getRes.Content.ReadFromJsonAsync<TaskItemDto>();
        Assert.Single(updatedTask!.Notes);
        Assert.Equal("This is a note", updatedTask.Notes[0].Content);
    }
}
