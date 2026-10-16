using WATR.Shared.DTOs;
using WATR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WATR.Web.Controllers;

public class UserController : ApiControllerBase
{
    private readonly UserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(UserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("GetMe: User={UserId}", userIdStr);
        
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var user = await _userService.GetUserAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("GetMe: User {UserId} not found in DB", userId);
            return NotFound();
        }

        return user;
    }
}
