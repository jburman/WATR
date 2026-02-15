using HotShallot.Shared.DTOs;
using HotShallot.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotShallot.Web.Controllers;

public class ProjectsController : ApiControllerBase
{
    private readonly ProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(ProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProjectDto>>> GetProjects(int page = 1, int pageSize = 20)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var result = await _projectService.GetUserProjectsPaginatedAsync(userId, page, pageSize);
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var project = await _projectService.CreateProjectAsync(request, userId);

        return CreatedAtAction(nameof(GetProjects), new { id = project.Id }, project);
    }
}
