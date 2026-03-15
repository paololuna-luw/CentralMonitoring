namespace CentralMonitoring.Domain.Entities;

public class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string MetricKey { get; set; } = null!;

    // Operators allowed: >, >=, <, <=, ==, !=
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; }

    // Minutes to look back for evaluation
    public int WindowMinutes { get; set; } = 5;

    public string Severity { get; set; } = "Critical";

    public Guid? HostId { get; set; }
    public string? SnmpIp { get; set; }
    public string? LabelContains { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
