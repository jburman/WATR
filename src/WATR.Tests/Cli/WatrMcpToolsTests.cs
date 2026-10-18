using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using WATR.Cli.Mcp;
using WATR.Cli.Services;
using WATR.Shared.DTOs;
using Xunit;

namespace WATR.Tests.Cli;

public class WatrMcpToolsTests
{
    [Fact]
    public void McpTools_ShouldHaveMcpServerToolTypeAttribute()
    {
        var attr = typeof(WatrMcpTools).GetCustomAttribute<McpServerToolTypeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void McpTools_ShouldExposeAllThirteenToolsWithDescriptions()
    {
        string[] expectedToolNames =
        [
            "watr_status",
            "watr_list_projects",
            "watr_create_project",
            "watr_set_project",
            "watr_list_tasks",
            "watr_search_tasks",
            "watr_get_task",
            "watr_create_task",
            "watr_record_decision",
            "watr_update_task",
            "watr_claim_task",
            "watr_release_task",
            "watr_complete_task",
            "watr_add_note"
        ];

        var methods = typeof(WatrMcpTools).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var tools = new Dictionary<string, (MethodInfo Method, McpServerToolAttribute ToolAttr, string Description)>();

        foreach (var method in methods)
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttr != null)
            {
                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                tools[toolAttr.Name ?? method.Name] = (method, toolAttr, descAttr?.Description ?? string.Empty);
            }
        }

        Assert.Equal(expectedToolNames.Length, tools.Count);

        foreach (var expectedName in expectedToolNames)
        {
            Assert.True(tools.ContainsKey(expectedName), $"Tool {expectedName} is missing from WatrMcpTools");
            var tool = tools[expectedName];
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"Tool {expectedName} lacks a Description");

            // Verify all method parameters have descriptions
            foreach (var param in tool.Method.GetParameters())
            {
                if (param.ParameterType == typeof(CancellationToken)) continue;
                var paramDesc = param.GetCustomAttribute<DescriptionAttribute>();
                Assert.True(paramDesc != null && !string.IsNullOrWhiteSpace(paramDesc.Description),
                    $"Parameter '{param.Name}' on tool '{expectedName}' is missing a [Description]");
            }
        }
    }

    [Fact]
    public async Task WatrStatus_UnconfiguredProject_ReturnsExpectedStatusDto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "watr_status_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configFile = Path.Combine(tempDir, "config.json");

        try
        {
            var config = new ConfigService(configFile);
            config.Save(new ConfigData { ServerUrl = "http://localhost:5000", DefaultProject = "MyProject" });

            // Using mock or dummy ApiClient with fake handler
            var mockHandler = new HttpMessageHandlerMock(req =>
            {
                if (req.RequestUri?.AbsolutePath.Contains("api/v1/user/me") == true)
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"id":"00000000-0000-0000-0000-000000000001","email":"test@watr.local","displayName":"Test Agent","createdAt":"2026-10-16T00:00:00Z"}""",
                            System.Text.Encoding.UTF8,
                            "application/json")
                    };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000/") };
            var api = new ApiClient(config, new TokenStore(), httpClient);
            var projectContext = new ProjectContextHelper(config, api);
            var mcpTools = new WatrMcpTools(api, config, projectContext);

            var status = await mcpTools.GetStatusAsync();

            Assert.Equal("http://localhost:5000", status.ServerUrl);
            Assert.Equal("Test Agent", status.UserDisplayName);
            Assert.Equal("test@watr.local", status.UserEmail);
            Assert.Equal("MyProject", status.ActiveProject);
            Assert.Equal("MyProject", status.DefaultProject);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private class HttpMessageHandlerMock : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public HttpMessageHandlerMock(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
