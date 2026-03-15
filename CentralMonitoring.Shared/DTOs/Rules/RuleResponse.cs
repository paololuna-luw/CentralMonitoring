namespace CentralMonitoring.Shared.DTOs.Rules;

public class RuleResponse
{
    public Guid Id { get; set; }
    public string MetricKey { get; set; } = null!;
    public string Operator { get; set; } = null!;
    public double Threshold { get; set; }
    public int WindowMinutes { get; set; }
    public string Severity { get; set; } = null!;
    public Guid? HostId { get; set; }
    public string? SnmpIp { get; set; }
    public string? LabelContains { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
