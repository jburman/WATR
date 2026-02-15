using Spectre.Console;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;

namespace HotShallot.Cli.Commands;

public class LogoutCommand : AsyncCommand<CommandSettings>
{
    private readonly TokenStore _tokenStore;
    private readonly ApiClient _api;

    public LogoutCommand(TokenStore tokenStore, ApiClient api)
    {
        _tokenStore = tokenStore;
        _api = api;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        var tokens = _tokenStore.LoadTokens();
        if (tokens != null && !string.IsNullOrEmpty(tokens.RefreshToken))
        {
            try
            {
                // Best effort revoke
                await _api.PostAsync<object>("api/auth/revoke", new HotShallot.Shared.DTOs.RevokeTokenRequest { RefreshToken = tokens.RefreshToken });
            }
            catch
            {
                // Ignore revocation errors during logout
            }
        }

        _tokenStore.ClearTokens();
        AnsiConsole.MarkupLine("[green]Logged out.[/]");
        return 0;
    }

}
