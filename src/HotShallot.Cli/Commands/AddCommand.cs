using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Enums;

namespace HotShallot.Cli.Commands;

public class AddCommand : AsyncCommand<AddCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;

    public AddCommand(ApiClient api, ConfigService config)
    {
        _api = api;
        _config = config;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<TITLE>")]
        public string Title { get; set; } = string.Empty;

        [CommandOption("-b|--body <BODY>")]
        public string? Body { get; set; }

        [CommandOption("-p|--project <NAME>")]
        public string? ProjectName { get; set; }

        [CommandOption("--priority <PRIORITY>")]
        public TaskPriority Priority { get; set; } = TaskPriority.None;

        [CommandOption("-t|--type <TYPE>")]
        public string? TaskType { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var configData = _config.Load();
        var projectName = settings.ProjectName ?? configData.DefaultProject;

        if (string.IsNullOrEmpty(projectName))
        {
            AnsiConsole.MarkupLine("[red]Error: No project specified.[/]");
            return 1;
        }

        var projects = await _api.GetAsync<PaginatedResponse<ProjectDto>>("api/v1/projects");
        var project = projects.Items.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Project '{projectName}' not found.[/]");
            return 3;
        }

        var body = settings.Body ?? "";
        if (body.StartsWith("@"))
        {
            var path = body[1..];
            if (File.Exists(path)) body = File.ReadAllText(path);
        }

        var request = new CreateTaskRequest
        {
            Title = settings.Title,
            Body = body,
            Priority = settings.Priority,
            TaskType = settings.TaskType
        };

        var task = await _api.PostAsync<TaskItemDto>($"api/v1/projects/{project.Id}/tasks", request);

        AnsiConsole.MarkupLine($"[green]✅ Created task {task.Id}[/]");
        return 0;
    }
}
