using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Commands;

public class StatusCommand : AsyncCommand<StatusCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;

    public StatusCommand(ApiClient api, ConfigService config)
    {
        _api = api;
        _config = config;
    }

    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _api.GetAsync<UserDto>("api/v1/user/me");
            var config = _config.Load();
            var localSettings = _config.GetLocalSettings();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
            grid.AddColumn(new GridColumn());

            grid.AddRow("[grey]Server:[/]", config.ServerUrl);
            grid.AddRow("[grey]User:[/]", $"{user.DisplayName} ({user.Email})");
            grid.AddRow("[grey]Local Project:[/]", localSettings?.ProjectName ?? "[yellow]Not Set (in this dir)[/]");

            AnsiConsole.Write(new Panel(grid).Header("HotShallot Status").BorderColor(Color.Orange1));
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
