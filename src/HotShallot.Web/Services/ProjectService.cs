using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HotShallot.Web.Services;

public class ProjectService
{
    private readonly IDbContextFactory<HotShallotDbContext> _dbFactory;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IDbContextFactory<HotShallotDbContext> dbFactory, ILogger<ProjectService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<List<ProjectDto>> GetUserProjectsAsync(Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Projects
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

    public async Task<PaginatedResponse<ProjectDto>> GetUserProjectsPaginatedAsync(Guid userId, int page, int pageSize)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Projects
            .Where(p => p.Members.Any(m => m.UserId == userId));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new PaginatedResponse<ProjectDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, Guid userId)
    {
        _logger.LogInformation("Creating project '{Name}' for user {UserId}", request.Name, userId);
        
        using var db = await _dbFactory.CreateDbContextAsync();
        var project = new Project
        {
            Id = Guid.CreateVersion7(),
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

        db.Projects.Add(project);
        await db.SaveChangesAsync();

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
