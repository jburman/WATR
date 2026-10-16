using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using WATR.Shared.Models;
using WATR.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace WATR.Web.Services;

public class ProjectService
{
    private readonly IDbContextFactory<WatrDbContext> _dbFactory;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IDbContextFactory<WatrDbContext> dbFactory, ILogger<ProjectService> logger)
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
                UpdatedAt = p.UpdatedAt,
                TaskTypes = p.TaskTypes
            })
            .ToListAsync();
    }

    public async Task<ProjectDto?> GetProjectAsync(Guid projectId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        var isMember = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (!isMember) return null;

        var p = await db.Projects.FindAsync(projectId);
        if (p == null) return null;

        return new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            CreatedByUserId = p.CreatedByUserId,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            TaskTypes = p.TaskTypes
        };
    }

    public async Task<ProjectDto> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (member == null || member.Role != ProjectRole.Admin)
        {
            throw new UnauthorizedAccessException("User is not an admin of this project.");
        }

        var project = await db.Projects.FindAsync(projectId);
        if (project == null) throw new Exception("Project not found");

        // Validate name uniqueness if changed
        if (project.Name != request.Name)
        {
            var exists = await db.Projects.AnyAsync(p => p.Name == request.Name);
            if (exists) throw new Exception("Project name already exists.");
        }

        // Validate TaskTypes removal
        var currentTypes = project.TaskTypes ?? new List<string>();
        var newTypes = request.TaskTypes ?? new List<string>();
        var removedTypes = currentTypes.Except(newTypes).ToList();

        if (removedTypes.Any())
        {
            // Simplify query for SQLite compatibility in tests
            var allTaskTypes = await db.TaskItems
                .Where(t => t.ProjectId == projectId)
                .Select(t => t.TaskType)
                .ToListAsync();

            var violations = allTaskTypes
                .Where(t => t != null && removedTypes.Contains(t))
                .Distinct()
                .ToList();

            if (violations.Any())
            {
                // We assume usedTypes elements are not null here because we filtered by removedTypes (strings)
                throw new InvalidOperationException($"Cannot remove task types that are in use: {string.Join(", ", violations)}");
            }
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.TaskTypes = newTypes;
        project.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedByUserId = project.CreatedByUserId,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            TaskTypes = project.TaskTypes
        };
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
                UpdatedAt = p.UpdatedAt,
                TaskTypes = p.TaskTypes
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
            UpdatedAt = DateTime.UtcNow,
            TaskTypes = request.TaskTypes != null && request.TaskTypes.Any() 
                ? request.TaskTypes 
                : new List<string> { "Epic", "Task" }
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
            UpdatedAt = project.UpdatedAt,
            TaskTypes = project.TaskTypes
        };
    }

    public async Task<List<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        // Verify user is a member
        var isMember = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (!isMember) throw new UnauthorizedAccessException("User is not a member of this project.");

        return await db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .Select(m => new ProjectMemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                Email = m.User.Email,
                Role = m.Role
            })
            .ToListAsync();
    }

    public async Task<List<string>> GetProjectAssigneesAsync(Guid projectId, Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        
        // Verify user is a member
        var isMember = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (!isMember) throw new UnauthorizedAccessException("User is not a member of this project.");

        var assignees = await db.TaskItems
            .Where(t => t.ProjectId == projectId && t.AssignedToName != null && t.AssignedToName != "")
            .Select(t => t.AssignedToName!)
            .Distinct()
            .ToListAsync();

        var memberNames = await db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.User.DisplayName)
            .Distinct()
            .ToListAsync();

        return assignees
            .Concat(memberNames)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }
}
