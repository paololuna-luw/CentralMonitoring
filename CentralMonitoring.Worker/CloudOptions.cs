namespace CentralMonitoring.Worker;

public class CloudOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "";
    public Guid InstanceId { get; set; }
    public string InstanceName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string OrganizationSlug { get; set; } = "";
    public string? Description { get; set; }
    public string? SyncApiKey { get; set; }
    public int SyncIntervalSeconds { get; set; } = 60;
}
