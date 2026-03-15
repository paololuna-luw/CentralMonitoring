namespace CentralMonitoring.Shared.DTOs.Alerts;

public class AlertDispatchConfig
{
    public string? WebhookUrl { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public int RetrySeconds { get; set; } = 60;
}
