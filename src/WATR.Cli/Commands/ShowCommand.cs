using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using WATR.Shared.Json;
using System.Text.Json;

namespace WATR.Cli.Commands;

public class ShowCommand : AsyncCommand<ShowCommand.Settings>
{
    private readonly ApiClient _api;

    public ShowCommand(ApiClient api)
    {
        _api = api;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        public Guid Id { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _api.GetAsync<TaskItemDto>($"api/v1/tasks/{settings.Id}");

            if (settings.Json)
            {
                var json = JsonSerializer.Serialize(task, AppJsonContext.Default.TaskItemDto);
                AnsiConsole.Write(new Text(json));
                return 0;
            }

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
            grid.AddColumn(new GridColumn());

            grid.AddRow(new Text("ID:", new Style(Color.Grey)).LeftJustified(), new Text(task.Id.ToString()));
            if (!string.IsNullOrWhiteSpace(task.TaskHandle))
                grid.AddRow(new Text("Handle:", new Style(Color.Grey)).LeftJustified(), new Text(task.TaskHandle));
            grid.AddRow(new Text("Type:", new Style(Color.Grey)).LeftJustified(), new Text(task.TaskType ?? "Task"));
            grid.AddRow(new Text("Title:", new Style(Color.Grey)).LeftJustified(), new Text(task.Title, new Style(Color.White, decoration: Decoration.Bold)));
            grid.AddRow(new Text("Status:", new Style(Color.Grey)).LeftJustified(), new Text(task.Status.ToString(), new Style(GetStatusColor(task.Status))));
            grid.AddRow(new Text("Priority:", new Style(Color.Grey)).LeftJustified(), new Text(task.Priority.ToString(), new Style(GetPriorityColor(task.Priority))));
            grid.AddRow(new Text("Assigned:", new Style(Color.Grey)).LeftJustified(), new Text(task.AssignedToDisplayName ?? "Unassigned"));
            if (task.Tags != null && task.Tags.Count > 0)
                grid.AddRow(new Text("Tags:", new Style(Color.Grey)).LeftJustified(), new Text(string.Join(", ", task.Tags)));
            if (task.SupersedesHandles != null && task.SupersedesHandles.Count > 0)
                grid.AddRow(new Text("Supersedes:", new Style(Color.Grey)).LeftJustified(), new Text(string.Join(", ", task.SupersedesHandles), new Style(Color.Aqua)));
            if (!string.IsNullOrWhiteSpace(task.SupersededByHandle))
                grid.AddRow(new Text("Superseded By:", new Style(Color.Grey)).LeftJustified(), new Text(task.SupersededByHandle, new Style(Color.Yellow)));
            grid.AddRow(new Text("Created:", new Style(Color.Grey)).LeftJustified(), new Text(task.CreatedAt.ToLocalTime().ToString("g")));

            var content = new Panel(grid)
                .Header("Task Details")
                .BorderColor(Color.Blue);

            AnsiConsole.Write(content);

            if (!string.IsNullOrWhiteSpace(task.Body))
            {
                AnsiConsole.MarkupLine("[bold]Description:[/]");
                AnsiConsole.Write(new Panel(new Text(task.Body)).Border(BoxBorder.Rounded));
            }

            if (task.Notes.Any())
            {
                AnsiConsole.MarkupLine("[bold]Notes:[/]");
                foreach (var note in task.Notes.OrderBy(n => n.CreatedAt))
                {
                    var notePanel = new Panel(new Text(note.Content))
                        .Header($"{note.AuthorDisplayName} - {note.CreatedAt.ToLocalTime():g}")
                        .Border(BoxBorder.None);
                    AnsiConsole.Write(notePanel);
                    AnsiConsole.WriteLine();
                }
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

    private static Color GetStatusColor(Shared.Enums.TaskItemStatus status) => status switch
    {
        Shared.Enums.TaskItemStatus.Open => Color.Blue,
        Shared.Enums.TaskItemStatus.InProgress => Color.Yellow,
        Shared.Enums.TaskItemStatus.Completed => Color.Green,
        Shared.Enums.TaskItemStatus.Blocked => Color.Red,
        Shared.Enums.TaskItemStatus.Cancelled => Color.Grey,
        Shared.Enums.TaskItemStatus.Obsolete => Color.Grey,
        _ => Color.White
    };

    private static Color GetPriorityColor(Shared.Enums.TaskPriority priority) => priority switch
    {
        Shared.Enums.TaskPriority.Critical => Color.Red,
        Shared.Enums.TaskPriority.High => Color.Orange1,
        Shared.Enums.TaskPriority.Medium => Color.Yellow,
        Shared.Enums.TaskPriority.Low => Color.Blue,
        _ => Color.White
    };
}
