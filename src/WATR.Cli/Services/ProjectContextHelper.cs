using Spectre.Console;
using WATR.Shared.DTOs;

namespace WATR.Cli.Services;

public class ProjectContextHelper
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;

    public ProjectContextHelper(ConfigService config, ApiClient api)
    {
        _config = config;
        _api = api;
    }

    public string? ResolveProjectName(string? explicitProjectName)
    {
        if (!string.IsNullOrWhiteSpace(explicitProjectName))
        {
            return explicitProjectName;
        }

        var localSettings = _config.GetLocalSettings();
        if (localSettings != null && !string.IsNullOrWhiteSpace(localSettings.ProjectName))
        {
            return localSettings.ProjectName;
        }

        var globalConfig = _config.Load();
        if (!string.IsNullOrWhiteSpace(globalConfig.DefaultProject))
        {
            return globalConfig.DefaultProject;
        }

        return null;
    }

    public Task<string> ResolveProjectAsync(string? explicitProjectName, CancellationToken cancellationToken = default)
    {
        var projectName = ResolveProjectName(explicitProjectName);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new InvalidOperationException("No active project context found. Please specify the 'project' parameter or configure the active project using 'watr_set_project' or 'watr config set-project <name>'.");
        }

        return Task.FromResult(projectName);
    }

    public async Task<string?> GetOrPromptProjectAsync(string? explicitProjectName, CancellationToken cancellationToken)
    {
        // 1. Explicit flag overrides all
        if (!string.IsNullOrEmpty(explicitProjectName))
        {
            return explicitProjectName;
        }

        // 2. Check local context
        var localSettings = _config.GetLocalSettings();
        if (localSettings != null && !string.IsNullOrEmpty(localSettings.ProjectName))
        {
            return localSettings.ProjectName;
        }

        // 3. Prompt user
        AnsiConsole.MarkupLine("[yellow]No project context found in this directory (looking for .watr/settings.json).[/]");
        var name = AnsiConsole.Ask<string>("Enter project name to link to this folder:");

        // 4. Validate
        try
        {
            var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
            var project = projects.Items.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                AnsiConsole.MarkupLine($"[red]Error: Project '{name}' not found.[/]");
                return null;
            }

            // 5. Save (preserve existing server url if any)
            var newSettings = localSettings ?? new LocalProjectSettings();
            newSettings.ProjectName = project.Name;
            _config.SaveLocalSettings(newSettings);
            
            AnsiConsole.MarkupLine($"[green]Linked directory to project: {project.Name}[/]");
            return project.Name;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error validating project: {ex.Message}[/]");
            return null;
        }
    }
}
