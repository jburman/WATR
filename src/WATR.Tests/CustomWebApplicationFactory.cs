using WATR.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WATR.Tests;

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
            services.RemoveAll<IDbContextFactory<WatrDbContext>>();
            services.RemoveAll<DbContextOptions<WatrDbContext>>();

            var dbName = "watr_test_" + Guid.NewGuid().ToString("N");
            var connStr = $"Data Source={dbName};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(connStr);
            _connection.Open();

            // Register DbContextFactory to use connection string with shared in-memory database
            services.AddDbContextFactory<WatrDbContext>(options =>
            {
                options.UseSqlite(connStr);
            });

            // We also need the Scoped adapter because Program.cs adds it manually? 
            // Program.cs has: builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<WatrDbContext>>().CreateDbContext());
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
