using System.Net;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var consoleEnabled = builder.Configuration.GetValue<bool?>("ConsoleLogging:Enabled") ?? true;
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration);
if (consoleEnabled) loggerConfig = loggerConfig.WriteTo.Console();

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ui",
        policy => policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MonitoringDbContext>("db");

builder.Services.AddDbContext<MonitoringDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    options.UseNpgsql(cs);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
    Log.Information("Applying database migrations on startup for CentralMonitoring.Api.");
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ui");

var apiKey = builder.Configuration["ApiKey"];
var isDev = builder.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    // Permit swagger UI and docs without key (optional).
    var path = context.Request.Path.Value;
    if (path is not null && (path.StartsWith("/swagger") || path.Equals("/")))
    {
        await next();
        return;
    }

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("API key not configured.");
        return;
    }

    // Dev convenience: allow localhost without key
    var remote = context.Connection.RemoteIpAddress;
    if (isDev && (remote is null || IPAddress.IsLoopback(remote)) &&
        !context.Request.Headers.ContainsKey("X-Api-Key"))
    {
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
        !string.Equals(provided, apiKey, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Invalid API key.");
        return;
    }

    await next();
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
