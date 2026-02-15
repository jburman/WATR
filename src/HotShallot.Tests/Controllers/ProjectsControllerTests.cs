using System.Net.Http.Json;
using HotShallot.Shared.DTOs;
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
    public async Task GetProjects_ShouldReturnUserProjects()
    {
        var user = await CreateUserAndAuthenticateAsync();
        
        // Initially empty? Or maybe "CreateProjectAsync" in base might have run? 
        // CreateUserAndAuthenticateAsync doesn't create projects.
        
        var response = await _client.GetAsync("api/v1/projects");
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProjectDto>>();
        Assert.Empty(page!.Items);

        // Create one
        await CreateProjectAsync(user.Id);

        // Fetch again
        response = await _client.GetAsync("api/v1/projects");
        page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProjectDto>>();
        Assert.Single(page!.Items);
    }
}
