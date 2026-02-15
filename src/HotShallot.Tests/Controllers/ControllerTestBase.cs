using System.Net.Http.Headers;
using HotShallot.Shared.Models;
using HotShallot.Tests.Helpers;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotShallot.Tests.Controllers;

public class ControllerTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory _factory;
    protected readonly HttpClient _client;
    protected readonly string _testKey = "TEST-KEY-FOR-INTEGRATION-TESTING-MUST-BE-LONG-ENOUGH";

    public ControllerTestBase(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    protected async Task<User> CreateUserAndAuthenticateAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user-{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HotShallotDbContext>>();
            using var db = await dbFactory.CreateDbContextAsync();
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var token = AuthHelper.GenerateJwtToken(user.Id, user.Email, _testKey);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return user;
    }

    protected async Task<Project> CreateProjectAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HotShallotDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Project-{Guid.NewGuid()}",
            Description = "Test Project",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        project.Members.Add(new ProjectMember
        {
            UserId = userId,
            Role = HotShallot.Shared.Enums.ProjectRole.Admin
        });

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return project;
    }
}
