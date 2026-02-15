using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HotShallot.Web.Services;

public class ProjectService
{
    private readonly HotShallotDbContext _db;

    public ProjectService(HotShallotDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProjectDto>> GetUserProjectsAsync(Guid userId)
    {
        return await _db.Projects
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedByUserId = p.CreatedByUserId,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, Guid userId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        project.Members.Add(new ProjectMember
        {
            UserId = userId,
            Role = ProjectRole.Admin
        });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedByUserId = project.CreatedByUserId,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
