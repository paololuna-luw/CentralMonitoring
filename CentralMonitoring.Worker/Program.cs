using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var consoleEnabled = builder.Configuration.GetValue<bool?>("ConsoleLogging:Enabled") ?? true;
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration);
if (consoleEnabled) loggerConfig = loggerConfig.WriteTo.Console();

Log.Logger = loggerConfig.CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddDbContext<MonitoringDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    options.UseNpgsql(cs);
});

builder.Services.Configure<CloudOptions>(builder.Configuration.GetSection("Cloud"));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<CloudSyncWorker>();

var host = builder.Build();
host.Run();
