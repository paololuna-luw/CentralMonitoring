namespace CentralMonitoring.CloudApi.DTOs.Centrals;

public class RegisterCentralRequest
{
    public string OrganizationName { get; set; } = "";
    public string OrganizationSlug { get; set; } = "";
    public Guid InstanceId { get; set; }
    public string InstanceName { get; set; } = "";
    public string? Description { get; set; }
    public string? ApiKey { get; set; }
}
