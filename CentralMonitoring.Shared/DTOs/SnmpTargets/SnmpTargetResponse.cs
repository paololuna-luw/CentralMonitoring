namespace CentralMonitoring.Shared.DTOs.SnmpTargets;

public class SnmpTargetResponse
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = null!;
    public string Version { get; set; } = null!;
    public string? Community { get; set; }
    public string? Name { get; set; }
    public string? Profile { get; set; }
    public Guid? HostId { get; set; }
    public string? HostName { get; set; }
    public string? Tags { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public List<SnmpMetricDto>? Metrics { get; set; }
}
