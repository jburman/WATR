using HotShallot.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotShallot.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SigningKey", "TEST-KEY-FOR-INTEGRATION-TESTING-MUST-BE-LONG-ENOUGH" }
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContextFactory registration
            services.RemoveAll<IDbContextFactory<HotShallotDbContext>>();
            services.RemoveAll<DbContextOptions<HotShallotDbContext>>();

            // Create open connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Register DbContextFactory to use the single open connection
            services.AddDbContextFactory<HotShallotDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // We also need the Scoped adapter because Program.cs adds it manually? 
            // Program.cs has: builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HotShallotDbContext>>().CreateDbContext());
            // This should still work as long as IDbContextFactory is registered.
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await base.DisposeAsync();
    }
}
