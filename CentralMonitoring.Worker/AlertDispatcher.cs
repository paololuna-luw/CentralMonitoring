using System.Net.Http.Json;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Worker;

public class AlertDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AlertDispatcher> _logger;
    private readonly AlertDispatchOptions _options;

    public AlertDispatcher(IHttpClientFactory httpClientFactory, ILogger<AlertDispatcher> logger, AlertDispatchOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options;
    }

    public async Task DispatchAsync(MonitoringDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl)) return;

        var pending = await db.AlertEvents
            .Where(a => !a.IsResolved && a.DispatchedAtUtc == null && a.DispatchAttempts < _options.MaxAttempts)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var client = _httpClientFactory.CreateClient();

        foreach (var alert in pending)
        {
            try
            {
                alert.DispatchAttempts += 1;

                var payload = new
                {
                    alert.Id,
                    alert.HostId,
                    alert.MetricKey,
                    alert.TriggerValue,
                    alert.Threshold,
                    alert.Severity,
                    alert.CreatedAtUtc,
                    alert.LastTriggerAtUtc,
                    alert.Occurrences
                };

                var resp = await client.PostAsJsonAsync(_options.WebhookUrl, payload, ct);
                if (resp.IsSuccessStatusCode)
                {
                    alert.DispatchedAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("Alert {AlertId} dispatched to {Webhook}", alert.Id, _options.WebhookUrl);
                }
                else
                {
                    _logger.LogWarning("Dispatch failed for alert {AlertId} (status {StatusCode})", alert.Id, resp.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dispatch error for alert {AlertId}", alert.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

public class AlertDispatchOptions
{
    public string WebhookUrl { get; set; } = "";
    public int MaxAttempts { get; set; } = 3;
}
