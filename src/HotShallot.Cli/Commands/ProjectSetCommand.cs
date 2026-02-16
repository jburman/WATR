using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Commands;

public class ProjectSetCommand : AsyncCommand<ProjectSetCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;

    public ProjectSetCommand(ApiClient api, ConfigService config)
    {
        _api = api;
        _config = config;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        public string Name { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
        var project = projects.Items.FirstOrDefault(p => p.Name.Equals(settings.Name, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Project '{settings.Name}' not found.[/]");
            return 3;
        }

        _config.SaveLocalProjectName(project.Name);

        AnsiConsole.MarkupLine($"[green]✅ Linked current directory to project: {project.Name}[/]");
        return 0;
    }
}
