namespace CentralMonitoring.Shared.DTOs.SnmpTargets;

public class SnmpMetricDto
{
    public string Key { get; set; } = null!;
    public string Oid { get; set; } = null!;
    public bool Enabled { get; set; } = true;
}
