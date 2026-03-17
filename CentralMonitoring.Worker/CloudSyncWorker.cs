using System.Net.Http.Json;
using System.Text.Json;
using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Domain.Entities;
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

        var latestSamples = await GetLatestSamplesForAlertsAsync(db, alerts, ct);

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
                labels = BuildAlertLabels(a, latestSamples),
                openedAtUtc = a.CreatedAtUtc,
                resolvedAtUtc = a.IsResolved ? a.LastTriggerAtUtc : (DateTime?)null
            }).ToList()
        };

        using var response = await http.PostAsJsonAsync($"api/v1/centrals/{_options.InstanceId}/alerts/sync", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<Dictionary<string, MetricSample>> GetLatestSamplesForAlertsAsync(MonitoringDbContext db, List<AlertEvent> alerts, CancellationToken ct)
    {
        var hostIds = alerts.Select(a => a.HostId).Where(id => id != Guid.Empty).Distinct().ToList();
        var metricKeys = alerts.Select(a => a.MetricKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (hostIds.Count == 0 || metricKeys.Count == 0)
            return new Dictionary<string, MetricSample>(StringComparer.OrdinalIgnoreCase);

        var minTimestamp = alerts.Min(a => a.CreatedAtUtc).AddDays(-1);
        var samples = await db.MetricSamples.AsNoTracking()
            .Where(m => hostIds.Contains(m.HostId) &&
                        metricKeys.Contains(m.MetricKey) &&
                        m.TimestampUtc >= minTimestamp)
            .OrderByDescending(m => m.TimestampUtc)
            .ToListAsync(ct);

        var latest = new Dictionary<string, MetricSample>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            var key = BuildAlertLookupKey(sample.HostId, sample.MetricKey);
            if (!latest.ContainsKey(key))
                latest[key] = sample;
        }

        return latest;
    }

    private object BuildAlertLabels(AlertEvent alert, IReadOnlyDictionary<string, MetricSample> latestSamples)
    {
        var labels = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["host_ip"] = alert.Host?.IpAddress,
            ["host_type"] = alert.Host?.Type,
            ["host_tags"] = alert.Host?.Tags,
            ["metric_display_name"] = BuildMetricDisplayName(alert.MetricKey)
        };

        if (TryGetLatestSample(alert, latestSamples, out var sample))
            MergeJsonObject(labels, sample.LabelsJson);

        MergeJsonObject(labels, alert.LabelsJson);

        if (alert.MetricKey.StartsWith("snmp_", StringComparison.OrdinalIgnoreCase))
        {
            labels["source_type"] = "snmp";
            labels["oid"] = ResolveSnmpOid(alert.MetricKey);
            if (!labels.ContainsKey("snmp_ip") && !string.IsNullOrWhiteSpace(alert.Host?.IpAddress))
                labels["snmp_ip"] = alert.Host.IpAddress;
        }
        else
        {
            labels["source_type"] = "agent";
        }

        if (TryParseSnmpIfIndex(alert.MetricKey, out var ifIndex))
            labels["if_index"] = ifIndex;

        return labels;
    }

    private static bool TryGetLatestSample(AlertEvent alert, IReadOnlyDictionary<string, MetricSample> latestSamples, out MetricSample sample)
    {
        return latestSamples.TryGetValue(BuildAlertLookupKey(alert.HostId, alert.MetricKey), out sample!);
    }

    private static string BuildAlertLookupKey(Guid hostId, string metricKey) => $"{hostId:N}:{metricKey}";

    private static void MergeJsonObject(IDictionary<string, object?> target, string? labelsJson)
    {
        if (string.IsNullOrWhiteSpace(labelsJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(labelsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in doc.RootElement.EnumerateObject())
                target[property.Name] = ConvertJsonValue(property.Value);
        }
        catch
        {
            // Ignore malformed labels and keep sync moving.
        }
    }

    private static object? ConvertJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object or JsonValueKind.Array => JsonSerializer.Deserialize<object>(value.GetRawText()),
            _ => value.GetRawText()
        };

    private static bool TryParseSnmpIfIndex(string metricKey, out int ifIndex)
    {
        ifIndex = 0;
        var lastUnderscore = metricKey.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore == metricKey.Length - 1)
            return false;

        return int.TryParse(metricKey[(lastUnderscore + 1)..], out ifIndex);
    }

    private static string BuildMetricDisplayName(string metricKey) =>
        metricKey switch
        {
            "cpu_usage_pct" => "CPU usage",
            "agent_cpu_usage_pct" => "Agent CPU usage",
            "mem_used_pct" => "Memory usage",
            "disk_used_pct" => "Disk usage",
            "service_up" => "Critical service state",
            "snmp_poll_failure" => "SNMP poll failure",
            _ when metricKey.StartsWith("snmp_ifOperStatus_", StringComparison.OrdinalIgnoreCase) => "SNMP interface status",
            _ when metricKey.StartsWith("snmp_ifInErrors_", StringComparison.OrdinalIgnoreCase) => "SNMP input errors",
            _ when metricKey.StartsWith("snmp_ifOutErrors_", StringComparison.OrdinalIgnoreCase) => "SNMP output errors",
            _ => metricKey
        };

    private static string? ResolveSnmpOid(string metricKey) =>
        metricKey switch
        {
            "snmp_sysUpTime" => "1.3.6.1.2.1.1.3.0",
            "snmp_ifInOctets_1" => "1.3.6.1.2.1.2.2.1.10.1",
            "snmp_ifOutOctets_1" => "1.3.6.1.2.1.2.2.1.16.1",
            "snmp_ifOperStatus_1" => "1.3.6.1.2.1.2.2.1.8.1",
            "snmp_ifInErrors_1" => "1.3.6.1.2.1.2.2.1.14.1",
            "snmp_ifOutErrors_1" => "1.3.6.1.2.1.2.2.1.20.1",
            "snmp_ifAdminStatus_1" => "1.3.6.1.2.1.2.2.1.7.1",
            "snmp_hrProcessorLoad_1" => "1.3.6.1.2.1.25.3.3.1.2.1",
            "snmp_hrProcessorLoad_2" => "1.3.6.1.2.1.25.3.3.1.2.2",
            _ => null
        };
}
