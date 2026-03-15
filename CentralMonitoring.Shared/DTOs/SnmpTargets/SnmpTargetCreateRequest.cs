namespace CentralMonitoring.Shared.DTOs.SnmpTargets;

public class SnmpTargetCreateRequest
{
    public string? IpAddress { get; set; }
    public Guid? HostId { get; set; }
    public string Version { get; set; } = "v2c"; // v1, v2c, v3 (solo v2c soportado en v1)
    public string? Community { get; set; } // v2c
    public string? Name { get; set; }
    public string? Profile { get; set; }
    public string? Tags { get; set; }
    public bool Enabled { get; set; } = true;
    public List<SnmpMetricDto>? Metrics { get; set; }
}
