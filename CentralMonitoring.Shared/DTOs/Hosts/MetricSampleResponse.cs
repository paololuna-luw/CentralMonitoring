namespace CentralMonitoring.Shared.DTOs.Metrics;

public class MetricSampleResponse
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
    public string MetricKey { get; set; } = null!;
    public DateTime TimestampUtc { get; set; }
    public double Value { get; set; }
    public string? LabelsJson { get; set; }
}
