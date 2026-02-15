using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;

namespace HotShallot.Cli.Commands;

public class ListCommand : AsyncCommand<ListCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;

    public ListCommand(ApiClient api, ConfigService config)
    {
        _api = api;
        _config = config;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-p|--project <NAME>")]
        public string? ProjectName { get; set; }

        [CommandOption("-s|--status <STATUS>")]
        public TaskItemStatus? Status { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var configData = _config.Load();
        var projectName = settings.ProjectName ?? configData.DefaultProject;

        if (string.IsNullOrEmpty(projectName))
        {
            AnsiConsole.MarkupLine("[red]Error: No project specified. Use --project or set a default project.[/]");
            return 1;
        }

        // Get project ID by name
        var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
        var project = projects.Items.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Project '{projectName}' not found.[/]");
            return 3;
        }

        var path = $"api/v1/projects/{project.Id}/tasks";
        if (settings.Status.HasValue) path += $"?status={settings.Status.Value}";

        var response = await _api.GetAsync<PaginatedResponse<TaskItemSummaryDto>>(path);

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Title");
        table.AddColumn("Status");
        table.AddColumn("Priority");
        table.AddColumn("Assigned To");
        table.AddColumn("Updated");

        foreach (var task in response.Items)
        {
            table.AddRow(
                task.Id.ToString()[..8],
                task.Title,
                GetStatusMarkup(task.Status),
                GetPriorityMarkup(task.Priority),
                task.AssignedToDisplayName ?? "[grey]Unassigned[/]",
                task.UpdatedAt.ToLocalTime().ToString("g")
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private string GetStatusMarkup(TaskItemStatus status) => status switch
    {
        TaskItemStatus.Open => "[blue]Open[/]",
        TaskItemStatus.InProgress => "[yellow]InProgress[/]",
        TaskItemStatus.Completed => "[green]Completed[/]",
        TaskItemStatus.Blocked => "[red]Blocked[/]",
        TaskItemStatus.Cancelled => "[grey]Cancelled[/]",
        _ => status.ToString()
    };

    private string GetPriorityMarkup(TaskPriority priority) => priority switch
    {
        TaskPriority.Critical => "[red]Critical[/]",
        TaskPriority.High => "[orange1]High[/]",
        TaskPriority.Medium => "[yellow]Medium[/]",
        TaskPriority.Low => "[green]Low[/]",
        _ => priority.ToString()
    };
}
