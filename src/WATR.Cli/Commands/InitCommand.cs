using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;

namespace WATR.Cli.Commands;

public class InitCommand : AsyncCommand<InitCommand.Settings>
{
    private readonly ConfigService _config;

    public InitCommand(ConfigService config)
    {
        _config = config;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SERVER_URL>")]
        public string ServerUrl { get; set; } = string.Empty;
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            AnsiConsole.MarkupLine("[red]Error: Server URL is required.[/]");
            return Task.FromResult(1);
        }

        var config = _config.Load();
        config.ServerUrl = settings.ServerUrl;
        _config.Save(config);

        AnsiConsole.MarkupLine($"[green]✅ Initialized WATR configuration.[/]");
        AnsiConsole.MarkupLine($"[grey]Server URL set to: {settings.ServerUrl}[/]");
        return Task.FromResult(0);
    }
}
