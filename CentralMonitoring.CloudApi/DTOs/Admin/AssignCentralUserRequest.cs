namespace CentralMonitoring.CloudApi.DTOs.Admin;

public class AssignCentralUserRequest
{
    public string Email { get; set; } = "";
    public string Role { get; set; } = "readonly";
}
