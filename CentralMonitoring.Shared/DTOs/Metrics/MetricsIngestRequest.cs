namespace CentralMonitoring.Shared.DTOs.Metrics;

public class MetricsIngestRequest
{
    public Guid HostId { get; set; }

    // si no viene, usamos UtcNow
    public DateTime? TimestampUtc { get; set; }

    public List<MetricPointDto> Metrics { get; set; } = new();
}
