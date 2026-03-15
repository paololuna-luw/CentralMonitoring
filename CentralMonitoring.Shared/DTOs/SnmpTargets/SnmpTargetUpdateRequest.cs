namespace CentralMonitoring.Shared.DTOs.SnmpTargets;

public class SnmpTargetUpdateRequest
{
    public Guid? HostId { get; set; }
    public string? Community { get; set; }
    public string? Name { get; set; }
    public string? Profile { get; set; }
    public string? Tags { get; set; }
    public bool? Enabled { get; set; }
    public List<SnmpMetricDto>? Metrics { get; set; }
}
