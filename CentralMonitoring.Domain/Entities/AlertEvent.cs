namespace CentralMonitoring.Domain.Entities;

public class AlertEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HostId { get; set; }
    public Host Host { get; set; } = null!;

    public string MetricKey { get; set; } = null!;
    public string? ContextKey { get; set; }
    public string? LabelsJson { get; set; }

    public double TriggerValue { get; set; }
    public double LastTriggerValue { get; set; }

    public double Threshold { get; set; }

    public string Severity { get; set; } = "Critical";

    public int Occurrences { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastTriggerAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsResolved { get; set; } = false;

    // Dispatch tracking
    public DateTime? DispatchedAtUtc { get; set; }
    public int DispatchAttempts { get; set; } = 0;
}
