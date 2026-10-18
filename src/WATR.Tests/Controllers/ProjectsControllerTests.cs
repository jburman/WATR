using System.Net.Http.Json;
using WATR.Shared.DTOs;
using WATR.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WATR.Tests.Controllers;

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
        Assert.Contains("Bug", project.TaskTypes);
        Assert.Contains("Decision", project.TaskTypes);
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
}
