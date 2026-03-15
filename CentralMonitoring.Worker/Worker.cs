using System.Net;
using CentralMonitoring.Domain.Entities;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SnmpSharpNet;
using System.Text.Json;

namespace CentralMonitoring.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeSpan _cooldown;
    private readonly TimeSpan _snmpPollInterval;
    private readonly List<SnmpMetricConfig> _snmpMetrics;
    private DateTime _nextSnmpPollUtc;
    private readonly int _snmpTimeoutMs;
    private readonly int _snmpRetries;
    private readonly bool _snmpGraceEnabled;
    private readonly int _snmpMaxFailures;
    private readonly int _snmpLogEvery;
    private readonly RetentionOptions _retention;
    private DateTime _nextRetentionUtc;
    private readonly AlertDispatchOptions _dispatchOptions;
    private readonly IHttpClientFactory _httpClientFactory;

    public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger, ILoggerFactory loggerFactory, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        var minutes = config.GetValue<int?>("Alerting:CooldownMinutes") ?? 10;
        _cooldown = TimeSpan.FromMinutes(minutes);

        var pollSeconds = config.GetValue<int?>("Snmp:PollIntervalSeconds") ?? 60;
        _snmpPollInterval = TimeSpan.FromSeconds(pollSeconds);
        _nextSnmpPollUtc = DateTime.UtcNow;
        _snmpTimeoutMs = config.GetValue<int?>("Snmp:TimeoutMs") ?? 2000;
        _snmpRetries = config.GetValue<int?>("Snmp:Retries") ?? 1;

        _snmpMetrics = config.GetSection("Snmp:Metrics").Get<List<SnmpMetricConfig>>() ?? new();
        if (_snmpMetrics.Count == 0)
        {
            _snmpMetrics = new List<SnmpMetricConfig>
            {
                new() { Key = "snmp_sysUpTime", Oid = "1.3.6.1.2.1.1.3.0" }
            };
        }
        _snmpGraceEnabled = config.GetValue<bool?>("Snmp:GraceEnabled") ?? false;
        _snmpMaxFailures = config.GetValue<int?>("Snmp:MaxConsecutiveFailures") ?? 120;
        _snmpLogEvery = Math.Max(1, config.GetValue<int?>("Snmp:LogEveryFailures") ?? 10);

        _retention = config.GetSection("Retention").Get<RetentionOptions>() ?? new RetentionOptions();
        _nextRetentionUtc = DateTime.UtcNow.AddMinutes(_retention.RunIntervalMinutes);

        _dispatchOptions = new AlertDispatchOptions
        {
            WebhookUrl = config.GetValue<string>("Alerting:WebhookUrl") ?? "",
            MaxAttempts = config.GetValue<int?>("Alerting:MaxDispatchAttempts") ?? 3
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

                var since = DateTime.UtcNow.AddMinutes(-3);

                var highCpuSamples = await db.MetricSamples
                    .Where(m => m.MetricKey == "cpu_usage"
                             && m.Value > 90
                             && m.TimestampUtc >= since)
                    .OrderByDescending(m => m.TimestampUtc)
                    .ToListAsync(stoppingToken);

                foreach (var sample in highCpuSamples)
                {
                    var existing = await db.AlertEvents.FirstOrDefaultAsync(a =>
                        a.HostId == sample.HostId &&
                        a.MetricKey == sample.MetricKey &&
                        !a.IsResolved,
                        stoppingToken);

                    var now = DateTime.UtcNow;

                    if (existing is null)
                    {
                        var alert = new AlertEvent
                        {
                            HostId = sample.HostId,
                            MetricKey = sample.MetricKey,
                            TriggerValue = sample.Value,
                            LastTriggerValue = sample.Value,
                            Threshold = 90,
                            Severity = "Critical",
                            Occurrences = 1,
                            CreatedAtUtc = now,
                            LastTriggerAtUtc = now
                        };

                        db.AlertEvents.Add(alert);

                        _logger.LogWarning(
                            "ALERT CREATED: Host {HostId} CPU {Value}",
                            sample.HostId,
                            sample.Value);
                    }
                    else
                    {
                        var elapsed = now - existing.LastTriggerAtUtc;
                        if (elapsed < _cooldown)
                        {
                            existing.LastTriggerAtUtc = now;
                            existing.LastTriggerValue = sample.Value;
                            existing.Occurrences += 1;
                            _logger.LogInformation(
                                "ALERT UPDATED: Host {HostId} CPU {Value} Occurrences {Occurrences}",
                                sample.HostId,
                                sample.Value,
                                existing.Occurrences);
                        }
                        else
                        {
                            var alert = new AlertEvent
                            {
                                HostId = sample.HostId,
                                MetricKey = sample.MetricKey,
                                TriggerValue = sample.Value,
                                LastTriggerValue = sample.Value,
                                Threshold = 90,
                                Severity = "Critical",
                                Occurrences = 1,
                                CreatedAtUtc = now,
                                LastTriggerAtUtc = now
                            };
                            db.AlertEvents.Add(alert);

                            _logger.LogWarning(
                                "ALERT RE-TRIGGERED after cooldown: Host {HostId} CPU {Value}",
                            sample.HostId,
                            sample.Value);
                        }
                    }
                }

                await db.SaveChangesAsync(stoppingToken);

                // Rules evaluation
                await EvaluateRules(db, stoppingToken);

                // SNMP polling
                if (DateTime.UtcNow >= _nextSnmpPollUtc)
                {
                    await PollSnmpTargets(db, stoppingToken);
                    _nextSnmpPollUtc = DateTime.UtcNow.Add(_snmpPollInterval);
                }

                // Retention
                if (_retention.Enabled && DateTime.UtcNow >= _nextRetentionUtc)
                {
                    await RunRetentionAsync(db, stoppingToken);
                    _nextRetentionUtc = DateTime.UtcNow.AddMinutes(_retention.RunIntervalMinutes);
                }

                // Dispatch alerts
                var dispatcher = new AlertDispatcher(_httpClientFactory, _loggerFactory.CreateLogger<AlertDispatcher>(), _dispatchOptions);
                await dispatcher.DispatchAsync(db, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Worker loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task PollSnmpTargets(MonitoringDbContext db, CancellationToken ct)
    {
        var targets = await db.SnmpTargets
            .Where(t => t.Enabled)
            .ToListAsync(ct);

        if (targets.Count == 0) return;

        foreach (var target in targets)
        {
            if (!string.Equals(target.Version, "v2c", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SNMP target {Ip} skipped (only v2c supported in v1).", target.IpAddress);
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.Community))
            {
                _logger.LogWarning("SNMP target {Ip} has no community set.", target.IpAddress);
                continue;
            }

            try
            {
                // Ensure we have a Host mapped to this target for FK consistency
                var hostId = await GetOrCreateHostForTarget(db, target, ct);

                using var agent = new UdpTarget(IPAddress.Parse(target.IpAddress), 161, _snmpTimeoutMs, _snmpRetries);
                var param = new AgentParameters(new OctetString(target.Community))
                {
                    Version = SnmpVersion.Ver2
                };
                var metricsToUse = GetMetricsForTarget(target);

                var pdu = new Pdu(PduType.Get);
                var enabledList = metricsToUse.Where(m => m.Enabled).ToList();
                if (enabledList.Count == 0)
                {
                    _logger.LogWarning("SNMP target {Ip} has no enabled metrics; skipping poll.", target.IpAddress);
                    continue;
                }
                foreach (var m in enabledList)
                    pdu.VbList.Add(m.Oid);

                var response = agent.Request(pdu, param) as SnmpV2Packet;

                if (response is null || response.Pdu.ErrorStatus != 0)
                {
                    await RegisterSnmpFailure(db, target, hostId, $"status {response?.Pdu.ErrorStatus}", ct);
                    continue;
                }

                var ts = DateTime.UtcNow;
                var enabledMetrics = enabledList;
                for (int i = 0; i < enabledMetrics.Count; i++)
                {
                    var cfg = enabledMetrics[i];
                    var vb = response.Pdu.VbList[i];
                    if (vb is null) continue;

                    double val;
                    if (vb.Value is Counter32 c32) val = c32.Value;
                    else if (vb.Value is Gauge32 g32) val = g32.Value;
                    else if (vb.Value is TimeTicks tt) val = tt.Milliseconds;
                    else if (vb.Value is Integer32 i32) val = i32.Value;
                    else if (vb.Value is Counter64 c64) val = (double)c64.Value;
                    else
                    {
                        _logger.LogDebug("SNMP value type not numeric for {Oid} on {Ip}", cfg.Oid, target.IpAddress);
                        continue;
                    }

                    db.MetricSamples.Add(new MetricSample
                    {
                        HostId = hostId,
                        MetricKey = cfg.Key,
                        TimestampUtc = ts,
                        Value = val,
                        LabelsJson = $"{{\"snmp_ip\":\"{target.IpAddress}\"}}"
                    });
                }
                await RegisterSnmpSuccess(db, target, hostId, ts, ct);
            }
            catch (Exception ex)
            {
                var host = await db.Hosts.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.IpAddress == target.IpAddress, ct);
                var fallbackHostId = host?.Id ?? Guid.Empty;
                await RegisterSnmpFailure(db, target, fallbackHostId, ex.Message, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RegisterSnmpSuccess(MonitoringDbContext db, SnmpTarget target, Guid hostId, DateTime now, CancellationToken ct)
    {
        target.ConsecutiveFailures = 0;
        target.LastSuccessUtc = now;

        var openFailure = await db.AlertEvents
            .FirstOrDefaultAsync(a =>
                a.HostId == hostId &&
                a.MetricKey == "snmp_poll_failure" &&
                !a.IsResolved,
                ct);
        if (openFailure is not null)
            openFailure.IsResolved = true;
    }

    private async Task RegisterSnmpFailure(MonitoringDbContext db, SnmpTarget target, Guid hostId, string reason, CancellationToken ct)
    {
        target.ConsecutiveFailures += 1;
        target.LastFailureUtc = DateTime.UtcNow;

        if (_snmpGraceEnabled && target.ConsecutiveFailures >= _snmpMaxFailures)
        {
            target.Enabled = false;
            _logger.LogWarning("SNMP target {Ip} disabled after {Failures} failures (reason: {Reason})",
                target.IpAddress, target.ConsecutiveFailures, reason);
        }
        else if (ShouldLogSnmpFailure(target.ConsecutiveFailures))
        {
            _logger.LogWarning("SNMP poll failed for {Ip} (failure {Failures}/{Max}). Reason: {Reason}",
                target.IpAddress, target.ConsecutiveFailures, _snmpMaxFailures, reason);
        }

        if (hostId != Guid.Empty)
        {
            var severity = target.ConsecutiveFailures >= 5 ? "Critical" : "Warning";
            await CreateOrUpdateAlert(
                db,
                hostId,
                "snmp_poll_failure",
                target.ConsecutiveFailures,
                0,
                severity,
                ct);
        }
    }

    private bool ShouldLogSnmpFailure(int consecutiveFailures) =>
        consecutiveFailures == 1 ||
        consecutiveFailures % _snmpLogEvery == 0 ||
        consecutiveFailures >= _snmpMaxFailures;

    private async Task EvaluateRules(MonitoringDbContext db, CancellationToken ct)
    {
        var rules = await db.Rules
            .Where(r => r.Enabled)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        var now = DateTime.UtcNow;

        foreach (var rule in rules)
        {
            var windowStart = now.AddMinutes(-rule.WindowMinutes);
            var snmpLabel = BuildSnmpLabelFilter(rule.SnmpIp);

            var q = db.MetricSamples.AsNoTracking()
                .Where(m => m.MetricKey == rule.MetricKey && m.TimestampUtc >= windowStart);

            if (rule.HostId.HasValue && rule.HostId != Guid.Empty)
                q = q.Where(m => m.HostId == rule.HostId.Value);

            if (!string.IsNullOrWhiteSpace(snmpLabel))
                q = q.Where(m => m.LabelsJson != null && m.LabelsJson.Contains(snmpLabel));

            if (!string.IsNullOrWhiteSpace(rule.LabelContains))
                q = q.Where(m => m.LabelsJson != null && m.LabelsJson.Contains(rule.LabelContains));

            var sample = await q
                .OrderByDescending(m => m.TimestampUtc)
                .FirstOrDefaultAsync(ct);

            if (sample is null) continue;

            if (!Evaluate(sample.Value, rule.Operator, rule.Threshold)) continue;

            var hostId = sample.HostId;
            if (hostId == Guid.Empty && rule.HostId.HasValue && rule.HostId != Guid.Empty)
                hostId = rule.HostId.Value;

            await CreateOrUpdateAlert(db, hostId, sample.MetricKey, sample.Value, rule.Threshold, rule.Severity, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private bool Evaluate(double value, string op, double threshold) =>
        op switch
        {
            ">" => value > threshold,
            ">=" => value >= threshold,
            "<" => value < threshold,
            "<=" => value <= threshold,
            "==" => Math.Abs(value - threshold) < 1e-9,
            "!=" => Math.Abs(value - threshold) >= 1e-9,
            _ => false
        };

    private async Task CreateOrUpdateAlert(
        MonitoringDbContext db,
        Guid hostId,
        string metricKey,
        double triggerValue,
        double threshold,
        string severity,
        CancellationToken ct)
    {
        var existing = await db.AlertEvents.FirstOrDefaultAsync(a =>
            a.HostId == hostId &&
            a.MetricKey == metricKey &&
            !a.IsResolved,
            ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            var alert = new AlertEvent
            {
                HostId = hostId,
                MetricKey = metricKey,
                TriggerValue = triggerValue,
                LastTriggerValue = triggerValue,
                Threshold = threshold,
                Severity = severity,
                Occurrences = 1,
                CreatedAtUtc = now,
                LastTriggerAtUtc = now
            };

            db.AlertEvents.Add(alert);

            _logger.LogWarning("ALERT CREATED: Host {HostId} {Metric} {Value}", hostId, metricKey, triggerValue);
        }
        else
        {
            var elapsed = now - existing.LastTriggerAtUtc;
            if (elapsed < _cooldown)
            {
                existing.LastTriggerAtUtc = now;
                existing.LastTriggerValue = triggerValue;
                existing.Occurrences += 1;
                _logger.LogInformation(
                    "ALERT UPDATED: Host {HostId} {Metric} {Value} Occurrences {Occurrences}",
                    hostId,
                    metricKey,
                    triggerValue,
                    existing.Occurrences);
            }
            else
            {
                var alert = new AlertEvent
                {
                    HostId = hostId,
                    MetricKey = metricKey,
                    TriggerValue = triggerValue,
                    LastTriggerValue = triggerValue,
                    Threshold = threshold,
                    Severity = severity,
                    Occurrences = 1,
                    CreatedAtUtc = now,
                    LastTriggerAtUtc = now
                };
                db.AlertEvents.Add(alert);

                _logger.LogWarning(
                    "ALERT RE-TRIGGERED after cooldown: Host {HostId} {Metric} {Value}",
                    hostId,
                    metricKey,
                    triggerValue);
            }
        }
    }
    private async Task<Guid> GetOrCreateHostForTarget(MonitoringDbContext db, SnmpTarget target, CancellationToken ct)
    {
        // Try to find existing host by IP
        var existing = await db.Hosts.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IpAddress == target.IpAddress, ct);

        if (existing is not null) return existing.Id;

        var host = new CentralMonitoring.Domain.Entities.Host
        {
            Name = target.Profile ?? target.IpAddress,
            IpAddress = target.IpAddress,
            Type = "Network",
            Tags = target.Tags,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Hosts.Add(host);
        await db.SaveChangesAsync(ct);
        return host.Id;
    }

    private List<SnmpMetricConfig> GetMetricsForTarget(SnmpTarget target)
    {
        return SnmpMetricHelper.ParseMetricsJson(target.MetricsJson, _snmpMetrics);
    }

    private async Task RunRetentionAsync(MonitoringDbContext db, CancellationToken ct)
    {
        var metricsCutoff = DateTime.UtcNow.AddDays(-_retention.MetricsDays);
        var alertsCutoff = DateTime.UtcNow.AddDays(-_retention.AlertsDays);
        var batch = Math.Max(1000, _retention.BatchSize);

        int totalMetrics = 0, totalAlerts = 0;

        if (_retention.MetricsDays > 0)
        {
            while (true)
            {
                var deleted = await db.MetricSamples
                    .Where(m => m.TimestampUtc < metricsCutoff)
                    .OrderBy(m => m.TimestampUtc)
                    .Take(batch)
                    .ExecuteDeleteAsync(ct);

                totalMetrics += deleted;
                if (deleted < batch) break;
            }
        }

        if (_retention.AlertsDays > 0)
        {
            while (true)
            {
                var deleted = await db.AlertEvents
                    .Where(a => a.CreatedAtUtc < alertsCutoff && a.IsResolved)
                    .OrderBy(a => a.CreatedAtUtc)
                    .Take(batch)
                    .ExecuteDeleteAsync(ct);

                totalAlerts += deleted;
                if (deleted < batch) break;
            }
        }

        if (totalMetrics > 0 || totalAlerts > 0)
        {
            _logger.LogInformation("Retention deleted {Metrics} metric rows and {Alerts} alert rows older than {MetricsDays}/{AlertsDays} days",
                totalMetrics, totalAlerts, _retention.MetricsDays, _retention.AlertsDays);
        }
        else
        {
            _logger.LogDebug("Retention run completed; no rows to delete (cutoffs metrics {MetricsCutoff}, alerts {AlertsCutoff})",
                metricsCutoff, alertsCutoff);
        }
    }

    private static string? BuildSnmpLabelFilter(string? snmpIp)
    {
        if (string.IsNullOrWhiteSpace(snmpIp)) return null;
        return $"\"snmp_ip\":\"{snmpIp.Trim()}\"";
    }
}

public class SnmpMetricConfig
{
    public string Key { get; set; } = null!;
    public string Oid { get; set; } = null!;
    public bool Enabled { get; set; } = true;
}

public class RetentionOptions
{
    public bool Enabled { get; set; } = false;
    public int MetricsDays { get; set; } = 30;
    public int AlertsDays { get; set; } = 90;
    public int RunIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 10000;
}

public static class SnmpMetricHelper
{
    public static List<SnmpMetricConfig> ParseMetricsJson(string? json, List<SnmpMetricConfig> fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try
        {
            var list = JsonSerializer.Deserialize<List<SnmpMetricConfig>>(json);
            if (list is null || list.Count == 0) return fallback;
            return list;
        }
        catch
        {
            return fallback;
        }
    }
}
