using Spectre.Console;
using Spectre.Console.Cli;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using WATR.Shared.Enums;

namespace WATR.Cli.Commands;

public class AddCommand : AsyncCommand<AddCommand.Settings>
{
    private readonly ApiClient _api;
    private readonly ConfigService _config;
    private readonly ProjectContextHelper _projectContext;

    public AddCommand(ApiClient api, ConfigService config, ProjectContextHelper projectContext)
    {
        _api = api;
        _config = config;
        _projectContext = projectContext;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<TITLE>")]
        public string Title { get; set; } = string.Empty;

        [CommandOption("-h|--handle <HANDLE>")]
        public string Handle { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(settings.Handle))
        {
            AnsiConsole.MarkupLine("[red]Error: Task handle is required (--handle).[/]");
            return 1;
        }

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

        var body = settings.Body ?? "";
        if (body.StartsWith("@"))
        {
            var path = body[1..];
            if (File.Exists(path)) body = File.ReadAllText(path);
        }

        var request = new CreateTaskRequest
        {
            Title = settings.Title,
            TaskHandle = settings.Handle,
            Body = body,
            Priority = settings.Priority,
            TaskType = settings.TaskType
        };

        var task = await _api.PostAsync<TaskItemDto>($"api/v1/projects/{project.Id}/tasks", request);

        AnsiConsole.MarkupLine($"[green]✅ Created task: {task.Id} (handle: {task.TaskHandle})[/]");
        return 0;
    }
}
