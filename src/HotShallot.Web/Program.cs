using HotShallot.Web.Components;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Fido2NetLib;
using Radzen;
using Microsoft.AspNetCore.Components;
using HotShallot.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using HotShallot.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();
builder.Services.AddSignalR();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllers();
//builder.Services.AddApplicationInsightsTelemetry(options =>
//{
//    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
//});

builder.Services.AddScoped(sp =>
{
    var navManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navManager.BaseUri) };
});

builder.Services.AddDbContextFactory<HotShallotDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HotShallotDbContext>>().CreateDbContext());

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = "HotShallot",
        ValidAudience = "HotShallot",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!))
    };
});

builder.Services.AddAuthorization(options =>
{
    // options.FallbackPolicy = options.DefaultPolicy;
});

// Configure FIDO2
builder.Services.AddFido2(options =>
{
    var serverDomain = builder.Configuration["Fido2:ServerDomain"];
    var origin = builder.Configuration["Fido2:Origin"];

    if (string.IsNullOrEmpty(serverDomain))
    {
        // Fallback for Azure App Service if WEBSITE_HOSTNAME is available
        var websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
        if (!string.IsNullOrEmpty(websiteHostname))
        {
            serverDomain = websiteHostname;
        }
        else
        {
            // Throwing here might crash startup, but it's better than obscure 500 errors later.
            // However, to keep the app alive for at least health checks, maybe we log?
            // Fido2 middleware requires valid options.
            throw new InvalidOperationException("Fido2:ServerDomain configuration is missing.");
        }
    }

    if (string.IsNullOrEmpty(origin))
    {
        // Fallback: construct from ServerDomain if possible
        if (!string.IsNullOrEmpty(serverDomain))
        {
            origin = $"https://{serverDomain}";
        }
        else
        {
            throw new InvalidOperationException("Fido2:Origin configuration is missing.");
        }
    }

    options.ServerDomain = serverDomain;
    options.ServerName = "HotShallot";
    options.Origins = new HashSet<string> { origin };
});

builder.Services.AddScoped<PassKeyService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<DeviceCodeService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectContextService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddMemoryCache();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<HotShallotDbContext>();

var app = builder.Build();

// Configure SQLite pragmas and run migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<HotShallotDbContext>();
    var config = services.GetRequiredService<IConfiguration>();
    
    var connString = config.GetConnectionString("DefaultConnection") ?? "Unknown";
    // Be careful with secrets in prod logs, but SQLite connection string is usually just a file path.
    logger.LogInformation("Using connection string: {ConnString}", connString);

    // Ensure directory exists if it's a file path
    try 
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connString);
        var dataSource = builder.DataSource;
        if (!string.IsNullOrEmpty(dataSource) && dataSource != ":memory:")
        {
            var dir = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                logger.LogInformation("Creating database directory: {Dir}", dir);
                Directory.CreateDirectory(dir);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error checking database directory.");
    }

    try 
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous = NORMAL;");
        db.Database.Migrate();
        logger.LogInformation("Database migration completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration.");
        // Rethrowing ensures the app doesn't start in a broken state, but for debugging on Azure we might want logs.
        throw; 
    }
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var error = exceptionHandlerPathFeature?.Error;

        var response = new HotShallot.Shared.DTOs.ErrorResponse
        {
            Error = "internal_error",
            Message = error?.Message ?? "An unexpected error occurred."
        };

        await context.Response.WriteAsJsonAsync(response);
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapHealthChecks("/health").AllowAnonymous();
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapHub<TaskHub>("/hubs/tasks");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
