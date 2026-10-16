using Microsoft.Extensions.Caching.Memory;
using WATR.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace WATR.Web.Services;

public class DeviceCodeService
{
    private readonly IMemoryCache _cache;
    private readonly JwtTokenService _jwtService;
    private readonly WatrDbContext _db;

    public DeviceCodeService(IMemoryCache cache, JwtTokenService jwtService, WatrDbContext db)
    {
        _cache = cache;
        _jwtService = jwtService;
        _db = db;
    }

    public void Initiate(string code)
    {
        var request = new DeviceCodeRequest
        {
            Code = code,
            CreatedAt = DateTime.UtcNow,
            Status = DeviceCodeStatus.Pending
        };

        _cache.Set($"device_code_{code}", request, TimeSpan.FromMinutes(5));
    }

    public DeviceCodeRequest? GetRequest(string code)
    {
        _cache.TryGetValue($"device_code_{code}", out DeviceCodeRequest? request);
        return request;
    }

    public async Task<bool> ApproveAsync(string code, Guid userId)
    {
        if (_cache.TryGetValue($"device_code_{code}", out DeviceCodeRequest? request) && request != null)
        {
            if (request.Status == DeviceCodeStatus.Pending)
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null) return false;

                request.Status = DeviceCodeStatus.Approved;
                request.UserId = user.Id;
                request.AccessToken = _jwtService.GenerateAccessToken(user);
                request.RefreshToken = _jwtService.GenerateRefreshToken();
                
                _cache.Set($"device_code_{code}", request, TimeSpan.FromMinutes(5));
                return true;
            }
        }
        return false;
    }
}

public class DeviceCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DeviceCodeStatus Status { get; set; }
    public Guid? UserId { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}

public enum DeviceCodeStatus
{
    Pending,
    Approved,
    Denied
}
