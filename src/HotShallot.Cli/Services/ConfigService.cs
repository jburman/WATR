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

    public string? GetLocalProjectName()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var settingsPath = Path.Combine(dir.FullName, ".shallot", "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize(json, CliJsonContext.Default.LocalProjectSettings);
                    if (!string.IsNullOrWhiteSpace(settings?.ProjectName))
                    {
                        return settings.ProjectName;
                    }
                }
                catch
                {
                    // Ignore errors reading local settings
                }
            }
            dir = dir.Parent;
        }
        return null;
    }

    public void SaveLocalProjectName(string projectName)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), ".shallot");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var settings = new LocalProjectSettings { ProjectName = projectName };
        var json = JsonSerializer.Serialize(settings, CliJsonContext.Default.LocalProjectSettings);
        File.WriteAllText(Path.Combine(dir, "settings.json"), json);
    }
}

public class ConfigData
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? DefaultProject { get; set; } // Deprecated, kept for backward compat if needed
}

public class LocalProjectSettings
{
    public string ProjectName { get; set; } = string.Empty;
}
