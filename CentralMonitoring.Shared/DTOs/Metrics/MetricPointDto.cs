namespace CentralMonitoring.Shared.DTOs.Metrics;

public class MetricPointDto
{
    public string Key { get; set; } = null!;
    public double Value { get; set; }
    public string? LabelsJson { get; set; }
}
