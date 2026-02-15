using System.Text.Json;
using HotShallot.Cli.Json;

namespace HotShallot.Cli.Services;

public class ConfigService
{
    private const string ConfigFileName = "config.json";
    private readonly string _configPath;

    public ConfigService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _configPath = Path.Combine(home, ".hotshallot", ConfigFileName);
    }

    public ConfigData Load()
    {
        if (!File.Exists(_configPath))
        {
            return new ConfigData { ServerUrl = "https://localhost:5001" };
        }

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize(json, CliJsonContext.Default.ConfigData) ?? new ConfigData();
    }

    public void Save(ConfigData data)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

        var json = JsonSerializer.Serialize(data, CliJsonContext.Default.ConfigData);
        File.WriteAllText(_configPath, json);
    }
}

public class ConfigData
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? DefaultProject { get; set; }
}
