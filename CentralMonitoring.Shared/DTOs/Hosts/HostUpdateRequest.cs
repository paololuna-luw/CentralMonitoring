using CentralMonitoring.Shared.Enums;

namespace CentralMonitoring.Shared.DTOs.Hosts;

public class HostUpdateRequest
{
    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public HostType? Type { get; set; }
    public string? Tags { get; set; }
    public bool? IsActive { get; set; }
}
