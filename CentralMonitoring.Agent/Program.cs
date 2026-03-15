using CentralMonitoring.Agent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "CentralMonitoring.Agent");
builder.Services.AddSystemd();
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LocalBatchBuffer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
