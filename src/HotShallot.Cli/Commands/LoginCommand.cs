using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Json;
using System.Diagnostics;
using System.Net.Http.Json;

namespace HotShallot.Cli.Commands;

public class LoginCommand : AsyncCommand<LoginCommand.Settings>
{
    private readonly ConfigService _config;
    private readonly TokenStore _tokenStore;

    public LoginCommand(ConfigService config, TokenStore tokenStore)
    {
        _config = config;
        _tokenStore = tokenStore;
    }

    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var configData = _config.Load();
        var code = GenerateCode();
        var serverUrl = configData.ServerUrl.TrimEnd('/');
        var deviceUrl = $"{serverUrl}/device";

        AnsiConsole.MarkupLine($"[yellow]Initiating login...[/]");

        using var http = new HttpClient { BaseAddress = new Uri(serverUrl + "/") };
        var initRequest = new DeviceLoginInitRequest { Code = code };
        var initResponse = await http.PostAsJsonAsync("api/auth/device-login-init", initRequest, AppJsonContext.Default.DeviceLoginInitRequest);
        
        if (!initResponse.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine("[red]Failed to initiate login.[/]");
            return 1;
        }

        var panel = new Panel(new Rows(
            new Text("Open your browser and enter this code:"),
            new Text(""),
            new Text(code, new Style(Color.Yellow, decoration: Decoration.Bold)).Centered(),
            new Text(""),
            new Text(deviceUrl).Centered()
        ));
        panel.Header = new PanelHeader(" HotShallot Login ");
        panel.Border = BoxBorder.Rounded;
        AnsiConsole.Write(panel);

        // Try to open browser
        try
        {
            Process.Start(new ProcessStartInfo(deviceUrl) { UseShellExecute = true });
        }
        catch { }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Waiting for authorization...", async ctx =>
            {
                var stopWatch = Stopwatch.StartNew();
                while (stopWatch.Elapsed < TimeSpan.FromMinutes(5))
                {
                    await Task.Delay(5000);
                    var statusResponse = await http.GetAsync($"api/auth/device-login-status?code={code}");
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var result = await statusResponse.Content.ReadFromJsonAsync(AppJsonContext.Default.DeviceLoginStatusResponse);
                        if (result?.Status == "approved")
                        {
                            _tokenStore.SaveTokens(result.AccessToken!, result.RefreshToken!);
                            return;
                        }
                    }
                }
                throw new Exception("Login timed out.");
            });

        AnsiConsole.MarkupLine("[green]✅ Successfully logged in![/]");
        return 0;
    }

    private string GenerateCode()
    {
        var random = new Random();
        var num = random.Next(10000000, 99999999).ToString();
        return $"{num.Substring(0, 4)}-{num.Substring(4, 4)}";
    }
}
