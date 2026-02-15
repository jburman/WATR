using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotShallot.Web.Controllers;

public class ProjectsController : ApiControllerBase
{
    private readonly HotShallotDbContext _db;

    public ProjectsController(HotShallotDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProjectDto>>> GetProjects(int page = 1, int pageSize = 20)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var query = _db.Projects
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

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

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

        return CreatedAtAction(nameof(GetProjects), new { id = project.Id }, new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedByUserId = project.CreatedByUserId,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        });
    }
}
