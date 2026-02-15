using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Commands;

public class ProjectCreateCommand : AsyncCommand<ProjectCreateCommand.Settings>
{
    private readonly ApiClient _api;

    public ProjectCreateCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("-d|--description <TEXT>")]
        public string Description { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var request = new CreateProjectRequest
            {
                Name = settings.Name,
                Description = settings.Description
            };

            var project = await _api.PostAsync<ProjectDto>("api/v1/projects", request);

            AnsiConsole.MarkupLine($"[green]✅ Created project '{project.Name}' (ID: {project.Id})[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
