using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using WATR.Shared.Json;
using System.Text.Json;

namespace WATR.Cli.Commands;

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

        [CommandOption("--tags <TAGS>")]
        public string? Tags { get; set; }

        [CommandOption("--superseded-by <HANDLE>")]
        public string? SupersededByHandle { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var body = settings.Body;
        if (body != null && body.StartsWith("@"))
        {
            var path = body[1..];
            if (File.Exists(path))
            {
                body = await File.ReadAllTextAsync(path, cancellationToken);
            }
        }

        var tags = string.IsNullOrWhiteSpace(settings.Tags)
            ? null
            : settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var request = new PatchTaskRequest
        {
            Title = settings.Title,
            Body = body,
            Status = settings.Status,
            Priority = settings.Priority,
            TaskHandle = settings.TaskHandle,
            TaskType = settings.TaskType,
            Tags = tags,
            SupersededByHandle = settings.SupersededByHandle
        };

        try
        {
            var task = await _api.PatchAsync<TaskItemDto>($"api/v1/tasks/{settings.Id}", request);

            if (settings.Json)
            {
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
