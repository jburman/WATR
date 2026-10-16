using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;

namespace WATR.Cli.Commands;

public class ClaimCommand : AsyncCommand<ClaimCommand.Settings>
{
    private readonly ApiClient _api;

    public ClaimCommand(ApiClient api)
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
            await _api.PostAsync($"api/v1/tasks/{settings.Id}/claim");
            AnsiConsole.MarkupLine($"[green]✅ Claimed task {settings.Id}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 4;
        }
    }
}
