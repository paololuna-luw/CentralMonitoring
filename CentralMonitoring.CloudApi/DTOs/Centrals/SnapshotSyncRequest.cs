namespace CentralMonitoring.CloudApi.DTOs.Centrals;

public class SnapshotSyncRequest
{
    public DateTime SnapshotTimestampUtc { get; set; }
    public int HostsTotal { get; set; }
    public int HostsActive { get; set; }
    public int AlertsOpen { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public object? Summary { get; set; }
}
