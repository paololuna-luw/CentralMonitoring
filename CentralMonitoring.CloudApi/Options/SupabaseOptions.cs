namespace CentralMonitoring.CloudApi.Options;

public class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public string JwtIssuer => Url.TrimEnd('/') + "/auth/v1";
    public string JwksUrl => JwtIssuer + "/.well-known/jwks.json";
}
