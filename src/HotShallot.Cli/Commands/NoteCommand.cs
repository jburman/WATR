using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;

namespace HotShallot.Cli.Commands;

public class NoteCommand : AsyncCommand<NoteCommand.Settings>
{
    private readonly ApiClient _api;

    public NoteCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        public string Id { get; set; } = string.Empty;

        [CommandArgument(1, "<MESSAGE>")]
        public string Message { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var message = settings.Message;
        if (message.StartsWith("@"))
        {
            var path = message[1..];
            if (File.Exists(path))
            {
                message = await File.ReadAllTextAsync(path, cancellationToken);
            }
        }

        try
        {
            await _api.PostAsync($"api/v1/tasks/{settings.Id}/notes", new CreateTaskNoteRequest { Content = message });
            AnsiConsole.MarkupLine($"[green]✅ Note added to task {settings.Id}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
