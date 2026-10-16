using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WATR.Cli.Json;

namespace WATR.Cli.Services;

public class TokenStore
{
    private const string TokenFileName = "tokens.dat";
    private readonly string _tokenPath;

    public TokenStore()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _tokenPath = Path.Combine(home, ".watr", TokenFileName);
    }

    public void SaveTokens(string accessToken, string refreshToken)
    {
        var directory = Path.GetDirectoryName(_tokenPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

        var data = JsonSerializer.Serialize(new Tokens { AccessToken = accessToken, RefreshToken = refreshToken }, CliJsonContext.Default.Tokens);
        
        if (OperatingSystem.IsWindows())
        {
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_tokenPath, encrypted);
        }
        else
        {
            // Fallback for non-windows (not secure, should use keyring/keychain in production)
            File.WriteAllText(_tokenPath, data);
        }
    }

    public Tokens? LoadTokens()
    {
        if (!File.Exists(_tokenPath)) return null;

        try
        {
            string data;
            if (OperatingSystem.IsWindows())
            {
                var encrypted = File.ReadAllBytes(_tokenPath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                data = Encoding.UTF8.GetString(decrypted);
            }
            else
            {
                data = File.ReadAllText(_tokenPath);
            }

            return JsonSerializer.Deserialize(data, CliJsonContext.Default.Tokens);
        }
        catch
        {
            return null;
        }
    }

    public void ClearTokens()
    {
        if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
    }
}

public class Tokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
