using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;
using WATR.Shared.Json;
using System.Text.Json;
using System.ComponentModel;

namespace WATR.Cli.Commands;

public class SearchCommand : AsyncCommand<SearchCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ProjectContextHelper _projectContext;

    public SearchCommand(ApiClient api, ProjectContextHelper projectContext)
    {
        _api = api;
        _projectContext = projectContext;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<QUERY>")]
        [Description("The semantic search query")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("-p|--project <NAME>")]
        [Description("Project name (defaults to active project in .watr/settings.json)")]
        public string? ProjectName { get; set; }

        [CommandOption("-s|--status <STATUS>")]
        [Description("Filter by task status")]
        public TaskItemStatus? Status { get; set; }

        [CommandOption("--type <TYPE>")]
        [Description("Filter by task type (e.g. Task, Epic, Bug, Decision)")]
        public string? TaskType { get; set; }

        [CommandOption("--tag <TAG>")]
        [Description("Filter by tag")]
        public string? Tag { get; set; }

        [CommandOption("-t|--top <N>")]
        [Description("Maximum number of results to return (default: 10)")]
        [DefaultValue(10)]
        public int Top { get; set; } = 10;

        [CommandOption("--min-score <SCORE>")]
        [Description("Minimum similarity score threshold (0.0 to 1.0, default: 0.25)")]
        [DefaultValue(0.25f)]
        public float MinScore { get; set; } = 0.25f;

        [CommandOption("--json")]
        [Description("Output raw JSON")]
        public bool Json { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectName = await _projectContext.GetOrPromptProjectAsync(settings.ProjectName, cancellationToken);

        if (string.IsNullOrEmpty(projectName))
        {
            return 1;
        }

        var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
        var project = projects.Items.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Project '{projectName}' not found.[/]");
            return 3;
        }

        var path = $"api/v1/projects/{project.Id}/tasks/search?q={Uri.EscapeDataString(settings.Query)}&top={settings.Top}&minScore={settings.MinScore}";
        if (settings.Status.HasValue)
        {
            path += $"&status={settings.Status.Value}";
        }
        if (!string.IsNullOrWhiteSpace(settings.TaskType))
        {
            path += $"&taskType={Uri.EscapeDataString(settings.TaskType)}";
        }
        if (!string.IsNullOrWhiteSpace(settings.Tag))
        {
            path += $"&tag={Uri.EscapeDataString(settings.Tag)}";
        }

        var results = await _api.GetAsync<List<TaskSearchResultDto>>(path);

        if (settings.Json)
        {
            var json = JsonSerializer.Serialize(results, AppJsonContext.Default.ListTaskSearchResultDto);
            AnsiConsole.Write(new Text(json));
            return 0;
        }

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No matching tasks found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Score");
        table.AddColumn("Handle");
        table.AddColumn("Title");
        table.AddColumn("Status");
        table.AddColumn("Priority");
        table.AddColumn("Assigned To");

        foreach (var item in results)
        {
            var task = item.Task;
            var scorePercent = (int)Math.Round(item.SimilarityScore * 100);
            var scoreMarkup = scorePercent >= 70 ? $"[green]{scorePercent}%[/]" :
                              scorePercent >= 40 ? $"[yellow]{scorePercent}%[/]" :
                              $"[grey]{scorePercent}%[/]";

            table.AddRow(
                scoreMarkup,
                task.TaskHandle ?? task.TaskNumber.ToString(),
                task.Title,
                GetStatusMarkup(task.Status),
                GetPriorityMarkup(task.Priority),
                task.AssignedToDisplayName ?? "[grey]Unassigned[/]"
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
        TaskItemStatus.Obsolete => "[grey]Obsolete[/]",
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
