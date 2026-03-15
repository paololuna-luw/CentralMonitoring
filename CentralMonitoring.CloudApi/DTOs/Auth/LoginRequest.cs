namespace CentralMonitoring.CloudApi.DTOs.Auth;

public class LoginRequest
{
    public string Provider { get; set; } = "google";
    public string? RedirectTo { get; set; }
}
