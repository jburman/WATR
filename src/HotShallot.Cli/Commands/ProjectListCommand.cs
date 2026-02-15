using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Commands;

public class ProjectListCommand : AsyncCommand<ProjectListCommand.Settings>
{
    private readonly ApiClient _api;

    public ProjectListCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");

            var table = new Table();
            table.AddColumn("ID");
            table.AddColumn("Name");
            table.AddColumn("Created");

            foreach (var project in response.Items)
            {
                table.AddRow(
                    project.Id.ToString()[..8],
                    project.Name,
                    project.CreatedAt.ToLocalTime().ToString("g")
                );
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
