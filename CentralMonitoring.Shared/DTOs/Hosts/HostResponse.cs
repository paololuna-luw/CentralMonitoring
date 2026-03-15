namespace CentralMonitoring.Shared.DTOs.Hosts;

public class HostResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Tags { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
