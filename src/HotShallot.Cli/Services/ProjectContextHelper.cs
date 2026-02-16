using Spectre.Console;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Services;

public class ProjectContextHelper
{
    private readonly ConfigService _config;
    private readonly ApiClient _api;

    public ProjectContextHelper(ConfigService config, ApiClient api)
    {
        _config = config;
        _api = api;
    }

    public async Task<string?> GetOrPromptProjectAsync(string? explicitProjectName, CancellationToken cancellationToken)
    {
        // 1. Explicit flag overrides all
        if (!string.IsNullOrEmpty(explicitProjectName))
        {
            return explicitProjectName;
        }

        // 2. Check local context
        var localProject = _config.GetLocalProjectName();
        if (!string.IsNullOrEmpty(localProject))
        {
            return localProject;
        }

        // 3. Prompt user
        AnsiConsole.MarkupLine("[yellow]No project context found in this directory (looking for .shallot/settings.json).[/]");
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

            // 5. Save
            _config.SaveLocalProjectName(project.Name);
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
