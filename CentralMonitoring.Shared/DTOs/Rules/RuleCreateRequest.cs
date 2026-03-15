namespace CentralMonitoring.Shared.DTOs.Rules;

public class RuleCreateRequest
{
    public string MetricKey { get; set; } = null!;
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; }
    public int WindowMinutes { get; set; } = 5;
    public string Severity { get; set; } = "Critical";
    public Guid? HostId { get; set; }
    public string? SnmpIp { get; set; }
    public string? LabelContains { get; set; }
    public bool Enabled { get; set; } = true;
}
