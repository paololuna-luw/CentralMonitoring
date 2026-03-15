namespace CentralMonitoring.Domain.Entities;

public class MetricSample
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HostId { get; set; }
    public Host Host { get; set; } = null!;

    // v1: simple, sin MetricDefinition
    public string MetricKey { get; set; } = null!;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public double Value { get; set; }

    // opcional: JSON string, v1 sin indexarlo
    public string? LabelsJson { get; set; }
}
