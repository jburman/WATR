using System.Text.Json.Serialization;

namespace WATR.Shared.DTOs;

public class RegisterOptionsRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginOptionsRequest
{
    public string Email { get; set; } = string.Empty;
}

public class RevokeTokenRequest
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class DeviceLoginInitRequest
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}

public class DeviceLoginStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
}
