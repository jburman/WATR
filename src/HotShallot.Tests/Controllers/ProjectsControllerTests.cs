using System.Net.Http.Json;
using HotShallot.Shared.DTOs;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotShallot.Tests.Controllers;

public class ProjectsControllerTests : ControllerTestBase
{
    public ProjectsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateProject_ShouldReturnCreated()
    {
        var user = await CreateUserAndAuthenticateAsync();
        
        var request = new CreateProjectRequest { Name = "New Project", Description = "Desc" };
        var response = await _client.PostAsJsonAsync("api/v1/projects", request);
        
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        
        Assert.NotNull(project);
        Assert.Equal("New Project", project.Name);
        Assert.Equal(user.Id, project.CreatedByUserId);
    }

    [Fact]
    public async Task CreateProject_ShouldHaveDefaultTaskTypes()
    {
        var user = await CreateUserAndAuthenticateAsync();
        
        var request = new CreateProjectRequest { Name = "Task Type Project", Description = "Desc" };
        var response = await _client.PostAsJsonAsync("api/v1/projects", request);
        
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        
        Assert.NotNull(project);
        Assert.Contains("Epic", project.TaskTypes);
        Assert.Contains("Task", project.TaskTypes);
    }

    [Fact]
    public async Task UpdateProject_ShouldAllowAddingTaskTypes()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id);

        var updateRequest = new UpdateProjectRequest
        {
            Name = project.Name,
            Description = project.Description,
            TaskTypes = new List<string> { "Epic", "Task", "Bug" }
        };

        var response = await _client.PutAsJsonAsync($"api/v1/projects/{project.Id}", updateRequest);
        response.EnsureSuccessStatusCode();

        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Contains("Bug", updatedProject!.TaskTypes);
    }

    [Fact]
    public async Task UpdateProject_ShouldAllowRemovingUnusedTaskTypes()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id); // Default: Epic, Task

        var updateRequest = new UpdateProjectRequest
        {
            Name = project.Name,
            Description = project.Description,
            TaskTypes = new List<string> { "Epic" } // Removed "Task"
        };

        var response = await _client.PutAsJsonAsync($"api/v1/projects/{project.Id}", updateRequest);
        response.EnsureSuccessStatusCode();

        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Single(updatedProject!.TaskTypes);
        Assert.DoesNotContain("Task", updatedProject.TaskTypes);
    }

    [Fact]
    public async Task UpdateProject_ShouldFailRemovingUsedTaskTypes()
    {
        var user = await CreateUserAndAuthenticateAsync();
        var project = await CreateProjectAsync(user.Id); // Default: Epic, Task

        // Create a task using "Task" type
        var taskReq = new CreateTaskRequest { Title = "My Task", TaskType = "Task" };
        var taskRes = await _client.PostAsJsonAsync($"api/v1/projects/{project.Id}/tasks", taskReq);
        taskRes.EnsureSuccessStatusCode();

        // Verify task is in DB with correct type
        using (var scope = _factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HotShallotDbContext>>();
            using var db = await dbFactory.CreateDbContextAsync();
            var taskInDb = await db.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == project.Id);
            Assert.NotNull(taskInDb);
            Assert.Equal("Task", taskInDb.TaskType);
        }

        // Try to remove "Task" type
        var updateRequest = new UpdateProjectRequest
        {
            Name = project.Name,
            Description = project.Description,
            TaskTypes = new List<string> { "Epic" } // Removed "Task"
        };

        var response = await _client.PutAsJsonAsync($"api/v1/projects/{project.Id}", updateRequest);
        
        if (response.StatusCode != System.Net.HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[TEST FAIL] Expected BadRequest, got {response.StatusCode}. Content: {content}");
        }

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Cannot remove task types that are in use", error!.Message);
    }
}
