using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;

namespace HotShallot.Cli.Commands;

public class DoneCommand : AsyncCommand<DoneCommand.Settings>
{
    private readonly ApiClient _api;

    public DoneCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        public string Id { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            await _api.PatchAsync($"api/v1/tasks/{settings.Id}", new PatchTaskRequest { Status = TaskItemStatus.Completed });
            AnsiConsole.MarkupLine($"[green]✅ Completed task {settings.Id}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
