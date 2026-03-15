using CentralMonitoring.Shared.Enums;

namespace CentralMonitoring.Shared.DTOs.Hosts;

public class HostCreateRequest
{
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public HostType Type { get; set; }
    public string? Tags { get; set; }
    public bool IsActive { get; set; } = true;
}
