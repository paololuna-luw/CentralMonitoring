namespace CentralMonitoring.Shared.DTOs.Rules;

public class RuleUpdateRequest
{
    public string? Operator { get; set; }
    public double? Threshold { get; set; }
    public int? WindowMinutes { get; set; }
    public string? Severity { get; set; }
    public Guid? HostId { get; set; }
    public string? SnmpIp { get; set; }
    public string? LabelContains { get; set; }
    public bool? Enabled { get; set; }
}
