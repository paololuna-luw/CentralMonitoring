namespace CentralMonitoring.Shared.DTOs.Alerts;

public class AlertResponse
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
    public string MetricKey { get; set; } = null!;
    public double TriggerValue { get; set; }
    public double LastTriggerValue { get; set; }
    public double Threshold { get; set; }
    public string Severity { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastTriggerAtUtc { get; set; }
    public int Occurrences { get; set; }
    public bool IsResolved { get; set; }
    public int DispatchAttempts { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
}
