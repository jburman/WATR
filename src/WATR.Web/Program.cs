using WATR.Web.Components;
using WATR.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Fido2NetLib;
using Radzen;
using Microsoft.AspNetCore.Components;
using WATR.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using WATR.Web.Hubs;

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

var resolvedConnString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/watr.db",
    builder.Environment.ContentRootPath);

builder.Services.AddDbContextFactory<WatrDbContext>(options =>
{
    options.UseSqlite(resolvedConnString);
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<WatrDbContext>>().CreateDbContext());

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
        ValidIssuer = "WATR",
        ValidAudience = "WATR",
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
    options.ServerName = "WATR";
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

builder.Services.AddHttpClient("ModelDownloader", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IOnnxEmbeddingService, OnnxEmbeddingService>();
builder.Services.AddSingleton<TaskEmbeddingQueue>();
builder.Services.AddHostedService<TaskEmbeddingWorker>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WatrDbContext>();

var app = builder.Build();

// Configure SQLite pragmas and run migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<WatrDbContext>();
    
    // Be careful with secrets in prod logs, but SQLite connection string is usually just a file path.
    logger.LogInformation("Using connection string: {ConnString}", resolvedConnString);

    // Ensure directory exists if it's a file path
    try 
    {
        var csBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(resolvedConnString);
        var dataSource = csBuilder.DataSource;
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
        // On Azure App Service Linux, /home is an SMB share which doesn't support WAL mode locking correctly.
        // We revert to default journaling to ensure stability.
        // db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
        // db.Database.ExecuteSqlRaw("PRAGMA synchronous = NORMAL;");
        
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

        var response = new WATR.Shared.DTOs.ErrorResponse
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

static string ResolveSqliteConnectionString(string connectionString, string contentRootPath)
{
    var csBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    var dataSource = csBuilder.DataSource;
    if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:" || Path.IsPathRooted(dataSource))
    {
        return connectionString;
    }

    var isAzureAppService =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
    var homeDir = Environment.GetEnvironmentVariable("HOME");

    if (isAzureAppService && !string.IsNullOrEmpty(homeDir))
    {
        // Store SQLite DB in $HOME/data (outside /home/site/wwwroot) so ZipDeploy / OneDeploy never wipes it.
        var persistentDataDir = Path.Combine(homeDir, "data");
        Directory.CreateDirectory(persistentDataDir);

        var dbFileName = Path.GetFileName(dataSource);
        var persistentDbPath = Path.Combine(persistentDataDir, dbFileName);
        var legacyWwwrootDbPath = Path.Combine(contentRootPath, dataSource);

        if (!File.Exists(persistentDbPath) && File.Exists(legacyWwwrootDbPath))
        {
            File.Copy(legacyWwwrootDbPath, persistentDbPath, overwrite: false);
        }

        csBuilder.DataSource = persistentDbPath;
        return csBuilder.ToString();
    }

    return connectionString;
}

public partial class Program { }
