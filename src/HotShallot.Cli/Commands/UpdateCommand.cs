using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;
using HotShallot.Shared.Json;
using System.Text.Json;

namespace HotShallot.Cli.Commands;

public class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
{
    private readonly ApiClient _api;

    public UpdateCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        public Guid Id { get; set; }

        [CommandOption("--title <TEXT>")]
        public string? Title { get; set; }

        [CommandOption("--body <TEXT>")]
        public string? Body { get; set; }

        [CommandOption("--status <STATUS>")]
        public TaskItemStatus? Status { get; set; }

        [CommandOption("--priority <PRIORITY>")]
        public TaskPriority? Priority { get; set; }

        [CommandOption("--handle <HANDLE>")]
        public string? TaskHandle { get; set; }

        [CommandOption("--type <TYPE>")]
        public string? TaskType { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var request = new PatchTaskRequest
        {
            Title = settings.Title,
            Body = settings.Body,
            Status = settings.Status,
            Priority = settings.Priority,
            TaskHandle = settings.TaskHandle,
            TaskType = settings.TaskType
        };

        try
        {
            await _api.PatchAsync($"api/v1/tasks/{settings.Id}", request);

            if (settings.Json)
            {
                var task = await _api.GetAsync<TaskItemDto>($"api/v1/tasks/{settings.Id}");
                var json = JsonSerializer.Serialize(task, AppJsonContext.Default.TaskItemDto);
                AnsiConsole.Write(new Text(json));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✅ Updated task {settings.Id.ToString()[..8]}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("404"))
            {
                AnsiConsole.MarkupLine($"[red]Error: Task {settings.Id} not found.[/]");
                return 3;
            }
            throw;
        }
    }
}
