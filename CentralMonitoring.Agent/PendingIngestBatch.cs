using CentralMonitoring.Shared.DTOs.Metrics;

namespace CentralMonitoring.Agent;

public class PendingIngestBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public DateTime EnqueuedAtUtc { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; } = 0;
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }
    public MetricsIngestRequest Payload { get; set; } = new();
}
