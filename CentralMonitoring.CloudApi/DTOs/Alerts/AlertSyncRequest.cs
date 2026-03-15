namespace CentralMonitoring.CloudApi.DTOs.Alerts;

public class AlertSyncRequest
{
    public List<AlertSyncItem> Alerts { get; set; } = new();
}

public class AlertSyncItem
{
    public Guid AlertId { get; set; }
    public Guid? HostId { get; set; }
    public string? HostName { get; set; }
    public string MetricKey { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Status { get; set; } = "";
    public double? TriggerValue { get; set; }
    public double? Threshold { get; set; }
    public object? Labels { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
