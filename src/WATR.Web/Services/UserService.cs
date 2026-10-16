using WATR.Shared.DTOs;
using WATR.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace WATR.Web.Services;

public class UserService
{
    private readonly IDbContextFactory<WatrDbContext> _dbFactory;

    public UserService(IDbContextFactory<WatrDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();
    }
}
