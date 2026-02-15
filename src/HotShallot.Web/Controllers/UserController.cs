using HotShallot.Shared.DTOs;
using HotShallot.Web.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotShallot.Web.Controllers;

public class UserController : ApiControllerBase
{
    private readonly HotShallotDbContext _db;

    public UserController(HotShallotDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return Unauthorized();
        var userId = Guid.Parse(userIdStr);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        };
    }
}
