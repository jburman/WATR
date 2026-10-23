using System.Reflection;

namespace WATR.Web.Services;

public static class AppVersionInfo
{
    public static string DisplayVersion { get; } = ResolveDisplayVersion();

    private static string ResolveDisplayVersion()
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(infoVersion))
        {
            var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            return $"v{version}";
        }

        var parts = infoVersion.Split('+', 2);
        var versionPart = parts[0];
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            var commitSha = parts[1].Length > 7 ? parts[1][..7] : parts[1];
            return $"v{versionPart} ({commitSha})";
        }

        return $"v{versionPart}";
    }
}
