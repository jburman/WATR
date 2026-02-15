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
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"]!;
    options.ServerName = "HotShallot";
    options.Origins = new HashSet<string> { builder.Configuration["Fido2:Origin"]! };
});

builder.Services.AddScoped<PassKeyService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<DeviceCodeService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectContextService>();
builder.Services.AddMemoryCache();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<HotShallotDbContext>();

var app = builder.Build();

// Configure SQLite pragmas and run migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HotShallotDbContext>();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
    db.Database.ExecuteSqlRaw("PRAGMA synchronous = NORMAL;");
    db.Database.Migrate();
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
