namespace CentralMonitoring.Domain.Entities;

public class SnmpTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IpAddress { get; set; } = null!;
    public string Version { get; set; } = "v2c"; // v1, v2c, v3 (solo v2c de momento)

    public string? Community { get; set; } // v2c

    // Campos para v3 (reservados)
    public string? SecurityName { get; set; }
    public string? AuthProtocol { get; set; }
    public string? AuthPassword { get; set; }
    public string? PrivProtocol { get; set; }
    public string? PrivPassword { get; set; }

    public string? Profile { get; set; }
    public string? Tags { get; set; }
    // Lista de métricas específicas para este target en formato JSON (array de { key, oid, enabled })
    public string? MetricsJson { get; set; }
    public bool Enabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ConsecutiveFailures { get; set; } = 0;
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
}
