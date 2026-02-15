using Fido2NetLib;
using Fido2NetLib.Objects;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using HotShallot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace HotShallot.Web.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly PassKeyService _passKeyService;
    private readonly DeviceCodeService _deviceCodeService;
    private readonly HotShallotDbContext _db;
    private readonly IMemoryCache _cache;

    public AuthController(PassKeyService passKeyService, DeviceCodeService deviceCodeService, HotShallotDbContext db, IMemoryCache cache)
    {
        _passKeyService = passKeyService;
        _deviceCodeService = deviceCodeService;
        _db = db;
        _cache = cache;
    }

    [HttpPost("passkey/register-options")]
    public async Task<IActionResult> RegisterOptions([FromBody] RegisterOptionsRequest request)
    {
        try
        {
            var user = await _db.Users
                .Include(u => u.PassKeys)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    DisplayName = request.DisplayName,
                    CreatedAt = DateTime.UtcNow
                };
                // We don't save yet, wait for successful registration
            }

            var existingKeys = user.PassKeys
                .Select(pk => new PublicKeyCredentialDescriptor(pk.CredentialId))
                .ToList();

            var options = _passKeyService.MakeCredentialOptions(user, existingKeys);

            _cache.Set($"reg_options_{request.Email}", options, TimeSpan.FromMinutes(5));

            return Ok(options);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("passkey/register")]
    public async Task<IActionResult> Register([FromBody] AuthenticatorAttestationRawResponse response, [FromQuery] string email, [FromQuery] string displayName)
    {
        try
        {
            if (!_cache.TryGetValue($"reg_options_{email}", out CredentialCreateOptions? options) || options == null)
            {
                return BadRequest("Registration options not found or expired");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    DisplayName = displayName,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            await _passKeyService.CreatePassKeyAsync(user, response, options);

            _cache.Remove($"reg_options_{email}");

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("passkey/login-options")]
    public async Task<IActionResult> LoginOptions([FromBody] LoginOptionsRequest request)
    {
        try
        {
            var options = await _passKeyService.MakeAssertionOptionsAsync(request.Email);
            _cache.Set($"login_options_{request.Email}", options, TimeSpan.FromMinutes(5));
            return Ok(options);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("passkey/login")]
    public async Task<IActionResult> Login([FromBody] AuthenticatorAssertionRawResponse response, [FromQuery] string email)
    {
        try
        {
            if (!_cache.TryGetValue($"login_options_{email}", out AssertionOptions? options) || options == null)
            {
                return BadRequest("Login options not found or expired");
            }

            var user = await _passKeyService.VerifyAssertionAsync(response, options);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.DisplayName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(claimsIdentity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddDays(14)
            });

            _cache.Remove($"login_options_{email}");

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("device-login-init")]
    public IActionResult DeviceLoginInit([FromBody] DeviceLoginInitRequest request)
    {
        _deviceCodeService.Initiate(request.Code);
        return Ok();
    }

    [HttpGet("device-login-status")]
    public IActionResult DeviceLoginStatus([FromQuery] string code)
    {
        var request = _deviceCodeService.GetRequest(code);
        if (request == null)
        {
            return NotFound("Device login request not found or expired");
        }

        if (request.Status == DeviceCodeStatus.Approved)
        {
            return Ok(new
            {
                status = "approved",
                accessToken = request.AccessToken,
                refreshToken = request.RefreshToken
            });
        }

        return Ok(new { status = "pending" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Ok();
    }
}

public class DeviceLoginInitRequest
{
    public string Code { get; set; } = string.Empty;
}
