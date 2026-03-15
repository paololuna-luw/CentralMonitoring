namespace CentralMonitoring.CloudApi.DTOs.Mobile;

public class RegisterDeviceTokenRequest
{
    public string Platform { get; set; } = "";
    public string DeviceToken { get; set; } = "";
}
