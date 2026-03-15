namespace CentralMonitoring.Domain.Entities;

public class Host
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;

    // Guardaremos el enum como string en la DB (ver DbContext)
    public string Type { get; set; } = null!;

    public string? Tags { get; set; }
    // Reservado: override de métricas por agente/host (JSON con lista de métricas)
    public string? MetricsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
