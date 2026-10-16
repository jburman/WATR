using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Spectre.Console.Cli;
using WATR.Cli.Mcp;
using WATR.Cli.Services;

namespace WATR.Cli.Commands;

public class McpCommand : AsyncCommand<McpCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        // Direct all logging to stderr so stdout remains clean JSON-RPC for MCP
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register WATR services
        builder.Services.AddSingleton<ConfigService>();
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<ProjectContextHelper>();
        builder.Services.AddSingleton<WatrMcpTools>();

        // Configure Model Context Protocol server over stdio
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
            {
                Name = "watr-mcp",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithTools<WatrMcpTools>();

        var app = builder.Build();

        await app.RunAsync(cancellationToken);
        return 0;
    }
}
