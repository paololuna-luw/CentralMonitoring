using System.Net.Http.Json;
using System.Text.Json;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralMonitoring.Worker;

public class CloudSyncWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudSyncWorker> _logger;
    private readonly CloudOptions _options;

    public CloudSyncWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<CloudOptions> options,
        ILogger<CloudSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Cloud sync disabled.");
            return;
        }

        _logger.LogInformation("Cloud sync enabled. InstanceId={InstanceId} BaseUrl={BaseUrl}",
            _options.InstanceId, _options.BaseUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
                var http = CreateHttpClient();

                await RegisterCentralAsync(http, stoppingToken);
                await SendHeartbeatAsync(http, stoppingToken);
                await SendSnapshotAsync(http, db, stoppingToken);
                await SyncAlertsAsync(http, db, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloud sync cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, _options.SyncIntervalSeconds)), stoppingToken);
        }
    }

    private bool IsEnabled() =>
        _options.Enabled &&
        !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
        _options.InstanceId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(_options.InstanceName) &&
        !string.IsNullOrWhiteSpace(_options.OrganizationName) &&
        !string.IsNullOrWhiteSpace(_options.OrganizationSlug);

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.SyncApiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", _options.SyncApiKey.Trim());
        return client;
    }

    private async Task RegisterCentralAsync(HttpClient http, CancellationToken ct)
    {
        var payload = new
        {
            organizationName = _options.OrganizationName,
            organizationSlug = _options.OrganizationSlug,
            instanceId = _options.InstanceId,
            instanceName = _options.InstanceName,
            description = _options.Description,
            apiKey = _options.SyncApiKey
        };

        using var response = await http.PostAsJsonAsync("api/v1/centrals/register", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendHeartbeatAsync(HttpClient http, CancellationToken ct)
    {
        var payload = new
        {
            lastSeenUtc = DateTime.UtcNow
        };

        using var response = await http.PostAsJsonAsync($"api/v1/centrals/{_options.InstanceId}/heartbeat", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendSnapshotAsync(HttpClient http, MonitoringDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fiveMinutesAgo = now.AddMinutes(-5);

        var hostsTotal = await db.Hosts.AsNoTracking().CountAsync(ct);
        var hostsActive = await db.MetricSamples.AsNoTracking()
            .Where(m => m.TimestampUtc >= fiveMinutesAgo)
            .Select(m => m.HostId)
            .Distinct()
            .CountAsync(ct);

        var openAlerts = await db.AlertEvents.AsNoTracking()
            .Where(a => !a.IsResolved)
            .ToListAsync(ct);

        var payload = new
        {
            snapshotTimestampUtc = now,
            hostsTotal,
            hostsActive,
            alertsOpen = openAlerts.Count,
            criticalAlerts = openAlerts.Count(a => a.Severity == "Critical"),
            warningAlerts = openAlerts.Count(a => a.Severity == "Warning"),
            summary = new
            {
                topOpenAlerts = openAlerts
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(10)
                    .Select(a => new
                    {
                        a.Id,
                        a.HostId,
                        a.MetricKey,
                        a.Severity,
                        a.LastTriggerValue,
                        a.LastTriggerAtUtc
                    })
                    .ToList()
            }
        };

        using var response = await http.PostAsJsonAsync($"api/v1/centrals/{_options.InstanceId}/snapshots", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task SyncAlertsAsync(HttpClient http, MonitoringDbContext db, CancellationToken ct)
    {
        var alerts = await db.AlertEvents.AsNoTracking()
            .Include(a => a.Host)
            .OrderByDescending(a => a.LastTriggerAtUtc)
            .Take(200)
            .ToListAsync(ct);

        var payload = new
        {
            alerts = alerts.Select(a => new
            {
                alertId = a.Id,
                hostId = a.HostId,
                hostName = a.Host?.Name,
                metricKey = a.MetricKey,
                severity = a.Severity,
                status = a.IsResolved ? "Resolved" : "Open",
                triggerValue = a.LastTriggerValue == 0 ? a.TriggerValue : a.LastTriggerValue,
                threshold = a.Threshold,
                labels = (object?)null,
                openedAtUtc = a.CreatedAtUtc,
                resolvedAtUtc = a.IsResolved ? a.LastTriggerAtUtc : (DateTime?)null
            }).ToList()
        };

        using var response = await http.PostAsJsonAsync($"api/v1/centrals/{_options.InstanceId}/alerts/sync", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }
}
