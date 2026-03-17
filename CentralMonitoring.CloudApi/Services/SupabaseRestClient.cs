using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using CentralMonitoring.CloudApi.DTOs.Admin;
using CentralMonitoring.CloudApi.DTOs.Alerts;
using CentralMonitoring.CloudApi.DTOs.Auth;
using CentralMonitoring.CloudApi.DTOs.Centrals;
using CentralMonitoring.CloudApi.DTOs.Mobile;
using CentralMonitoring.CloudApi.Options;
using Microsoft.Extensions.Options;

namespace CentralMonitoring.CloudApi.Services;

public class SupabaseRestClient
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CurrentAlertFreshness = TimeSpan.FromHours(6);

    public SupabaseRestClient(HttpClient httpClient, IOptions<SupabaseOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/rest/v1/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("apikey", _options.ServiceRoleKey);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
    }

    public async Task<RegisterCentralResult> RegisterCentralAsync(RegisterCentralRequest request, CancellationToken ct)
    {
        var organization = await UpsertOrganizationAsync(request.OrganizationName, request.OrganizationSlug, ct);
        var central = await UpsertCentralAsync(organization.Id, request, ct);
        return new RegisterCentralResult
        {
            OrganizationId = organization.Id,
            CentralId = central.Id,
            InstanceId = central.InstanceId,
            InstanceName = central.InstanceName
        };
    }

    public async Task<bool> HeartbeatAsync(Guid instanceId, DateTime lastSeenUtc, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Patch, $"central_instances?instance_id=eq.{instanceId}");
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(new
        {
            last_seen_utc = lastSeenUtc,
            is_active = true
        });

        using var resp = await _httpClient.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> InsertSnapshotAsync(Guid instanceId, SnapshotSyncRequest request, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null) return false;

        var payload = new
        {
            central_instance_id = centralId,
            snapshot_timestamp_utc = request.SnapshotTimestampUtc,
            hosts_total = request.HostsTotal,
            hosts_active = request.HostsActive,
            alerts_open = request.AlertsOpen,
            critical_alerts = request.CriticalAlerts,
            warning_alerts = request.WarningAlerts,
            summary_json = request.Summary
        };

        using var resp = await PostAsync("central_snapshots", payload, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<AlertSyncResult> UpsertAlertsAsync(Guid instanceId, AlertSyncRequest request, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null)
            return new AlertSyncResult { InsertedOrUpdated = 0, Failed = request.Alerts.Count };

        if (request.Alerts.Count == 0)
            return new AlertSyncResult();

        var payload = request.Alerts.Select(a => new
        {
            central_instance_id = centralId,
            source_alert_id = a.AlertId,
            host_id = a.HostId,
            host_name = a.HostName,
            metric_key = a.MetricKey,
            severity = a.Severity,
            status = a.Status,
            trigger_value = a.TriggerValue,
            threshold = a.Threshold,
            labels_json = a.Labels,
            opened_at_utc = a.OpenedAtUtc,
            resolved_at_utc = a.ResolvedAtUtc,
            last_synced_at_utc = DateTime.UtcNow
        }).ToList();

        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "cloud_alerts?on_conflict=central_instance_id,source_alert_id");
        reqMsg.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        reqMsg.Content = JsonContent.Create(payload);

        using var resp = await _httpClient.SendAsync(reqMsg, ct);
        if (!resp.IsSuccessStatusCode)
            return new AlertSyncResult { InsertedOrUpdated = 0, Failed = request.Alerts.Count };

        return new AlertSyncResult { InsertedOrUpdated = request.Alerts.Count, Failed = 0 };
    }

    public async Task<List<UserCentralSummary>> GetUserCentralsAsync(Guid userId, CancellationToken ct)
    {
        var url = $"vw_user_centrals?user_id=eq.{userId}";
        return await _httpClient.GetFromJsonAsync<List<UserCentralSummary>>(url, JsonOptions, ct) ?? new List<UserCentralSummary>();
    }

    public async Task<AppUserProfile?> GetAppUserAsync(Guid userId, CancellationToken ct)
    {
        var url = $"app_users?id=eq.{userId}&select=id,email,full_name,is_active,created_at_utc&limit=1";
        var rows = await _httpClient.GetFromJsonAsync<List<AppUserProfile>>(url, JsonOptions, ct) ?? new List<AppUserProfile>();
        return rows.FirstOrDefault();
    }

    public async Task<UserCentralSummary?> GetUserCentralByInstanceAsync(Guid userId, Guid instanceId, CancellationToken ct)
    {
        var url = $"vw_user_centrals?user_id=eq.{userId}&instance_id=eq.{instanceId}&limit=1";
        var rows = await _httpClient.GetFromJsonAsync<List<UserCentralSummary>>(url, JsonOptions, ct) ?? new List<UserCentralSummary>();
        return rows.FirstOrDefault();
    }

    public async Task<AppUserProfile?> GetAppUserByEmailAsync(string email, CancellationToken ct)
    {
        var url = $"app_users?email=eq.{Uri.EscapeDataString(email.Trim())}&select=id,email,full_name,is_active,created_at_utc&limit=1";
        var rows = await _httpClient.GetFromJsonAsync<List<AppUserProfile>>(url, JsonOptions, ct) ?? new List<AppUserProfile>();
        return rows.FirstOrDefault();
    }

    public async Task<CentralSnapshotSummary?> GetLatestSummaryAsync(Guid instanceId, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null) return null;

        var url = $"central_snapshots?central_instance_id=eq.{centralId}&order=snapshot_timestamp_utc.desc&limit=1";
        var rows = await _httpClient.GetFromJsonAsync<List<CentralSnapshotSummary>>(url, JsonOptions, ct) ?? new List<CentralSnapshotSummary>();
        return rows.FirstOrDefault();
    }

    public async Task<List<CloudAlertSummary>> GetUserAlertsAsync(Guid userId, CancellationToken ct)
    {
        var centrals = await GetUserCentralsAsync(userId, ct);
        if (centrals.Count == 0) return new List<CloudAlertSummary>();

        var centralIds = string.Join(",", centrals.Select(c => c.CentralId));
        var url = $"cloud_alerts?central_instance_id=in.({centralIds})&status=in.(Open,Acked)&order=opened_at_utc.desc";
        return await _httpClient.GetFromJsonAsync<List<CloudAlertSummary>>(url, JsonOptions, ct) ?? new List<CloudAlertSummary>();
    }

    public async Task<List<CloudAlertSummary>> GetUserCurrentAlertsAsync(Guid userId, CancellationToken ct)
    {
        var alerts = await GetUserAlertsAsync(userId, ct);
        var cutoff = DateTime.UtcNow.Subtract(CurrentAlertFreshness);

        return alerts
            .Where(a => a.LastSyncedAtUtc >= cutoff)
            .GroupBy(BuildOperationalAlertKey, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(a => a.LastSyncedAtUtc)
                .ThenByDescending(a => a.OpenedAtUtc)
                .First())
            .OrderByDescending(a => a.OpenedAtUtc)
            .ToList();
    }

    public async Task<MobileDeviceTokenRecord> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var payload = new[]
        {
            new
            {
                user_id = userId,
                platform = request.Platform.Trim().ToLowerInvariant(),
                device_token = request.DeviceToken.Trim(),
                is_active = true,
                last_seen_utc = DateTime.UtcNow
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "mobile_device_tokens?on_conflict=device_token");
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = JsonContent.Create(payload);

        using var resp = await _httpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var rows = await resp.Content.ReadFromJsonAsync<List<MobileDeviceTokenRecord>>(JsonOptions, ct) ?? new List<MobileDeviceTokenRecord>();
        return rows[0];
    }

    public async Task<bool> DeleteDeviceTokenAsync(Guid userId, Guid tokenId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"mobile_device_tokens?id=eq.{tokenId}&user_id=eq.{userId}");
        req.Headers.Add("Prefer", "return=representation");

        using var resp = await _httpClient.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<CloudAlertSummary?> SetAlertStatusAsync(Guid userId, Guid alertId, string newStatus, CancellationToken ct)
    {
        var userAlerts = await GetUserAlertsAsync(userId, ct);
        var target = userAlerts.FirstOrDefault(a => a.Id == alertId);
        if (target is null) return null;

        using var req = new HttpRequestMessage(HttpMethod.Patch, $"cloud_alerts?id=eq.{alertId}");
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(new
        {
            status = newStatus,
            resolved_at_utc = string.Equals(newStatus, "Resolved", StringComparison.OrdinalIgnoreCase)
                ? DateTime.UtcNow
                : (DateTime?)null,
            last_synced_at_utc = DateTime.UtcNow
        });

        using var resp = await _httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var rows = await resp.Content.ReadFromJsonAsync<List<CloudAlertSummary>>(JsonOptions, ct) ?? new List<CloudAlertSummary>();
        return rows.FirstOrDefault();
    }

    private static string BuildOperationalAlertKey(CloudAlertSummary alert)
    {
        var parts = new List<string>
        {
            alert.CentralInstanceId.ToString("N"),
            alert.HostId?.ToString("N") ?? "",
            alert.MetricKey
        };

        foreach (var propertyName in new[] { "service", "kind", "drive", "iface", "process", "pid", "snmp_ip", "oid", "if_index" })
        {
            if (TryGetLabelString(alert, propertyName, out var value) && !string.IsNullOrWhiteSpace(value))
                parts.Add($"{propertyName}={value}");
        }

        return string.Join("|", parts);
    }

    private static bool TryGetLabelString(CloudAlertSummary alert, string propertyName, out string? value)
    {
        value = null;
        if (alert.LabelsJson is null || alert.LabelsJson.Value.ValueKind != JsonValueKind.Object)
            return false;

        if (!alert.LabelsJson.Value.TryGetProperty(propertyName, out var property))
            return false;

        value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => property.GetRawText()
        };

        return true;
    }

    public async Task<List<CentralUserAccessSummary>> GetCentralUsersAsync(Guid instanceId, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null) return new List<CentralUserAccessSummary>();

        var url = $"central_user_access?central_instance_id=eq.{centralId}&select=id,central_instance_id,user_id,role,is_active,created_at_utc,app_users(id,email,full_name,is_active)";
        return await _httpClient.GetFromJsonAsync<List<CentralUserAccessSummary>>(url, JsonOptions, ct) ?? new List<CentralUserAccessSummary>();
    }

    public async Task<CentralUserAccessSummary?> AssignUserToCentralAsync(Guid instanceId, AssignCentralUserRequest request, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null) return null;

        var user = await GetAppUserByEmailAsync(request.Email, ct);
        if (user is null) return null;

        var payload = new[]
        {
            new
            {
                central_instance_id = centralId,
                user_id = user.Id,
                role = request.Role.Trim().ToLowerInvariant(),
                is_active = true
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "central_user_access?on_conflict=central_instance_id,user_id");
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = JsonContent.Create(payload);

        using var resp = await _httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var rows = await resp.Content.ReadFromJsonAsync<List<CentralUserAccessFlat>>(JsonOptions, ct) ?? new List<CentralUserAccessFlat>();
        var row = rows.FirstOrDefault();
        if (row is null) return null;

        return new CentralUserAccessSummary
        {
            Id = row.Id,
            CentralInstanceId = row.CentralInstanceId,
            UserId = row.UserId,
            Role = row.Role,
            IsActive = row.IsActive,
            CreatedAtUtc = row.CreatedAtUtc,
            AppUser = user
        };
    }

    public async Task<bool> RemoveUserFromCentralAsync(Guid instanceId, Guid targetUserId, CancellationToken ct)
    {
        var centralId = await GetCentralDbIdAsync(instanceId, ct);
        if (centralId is null) return false;

        using var req = new HttpRequestMessage(HttpMethod.Delete, $"central_user_access?central_instance_id=eq.{centralId}&user_id=eq.{targetUserId}");
        req.Headers.Add("Prefer", "return=representation");

        using var resp = await _httpClient.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public string BuildLoginUrl(LoginRequest request, string? fallbackRedirectTo)
    {
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "google" : request.Provider.Trim().ToLowerInvariant();
        var redirectTo = string.IsNullOrWhiteSpace(request.RedirectTo) ? fallbackRedirectTo : request.RedirectTo;

        var builder = new UriBuilder(_options.JwtIssuer + "/authorize");
        var query = HttpUtility.ParseQueryString(builder.Query);
        query["provider"] = provider;

        if (!string.IsNullOrWhiteSpace(redirectTo))
            query["redirect_to"] = redirectTo;

        builder.Query = query.ToString() ?? string.Empty;
        return builder.ToString();
    }

    private async Task<SupabaseOrganization> UpsertOrganizationAsync(string name, string slug, CancellationToken ct)
    {
        var payload = new[]
        {
            new
            {
                name,
                slug,
                is_active = true
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "organizations?on_conflict=slug");
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = JsonContent.Create(payload);

        using var resp = await _httpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var rows = await resp.Content.ReadFromJsonAsync<List<SupabaseOrganization>>(JsonOptions, ct) ?? new List<SupabaseOrganization>();
        return rows[0];
    }

    private async Task<SupabaseCentral> UpsertCentralAsync(Guid organizationId, RegisterCentralRequest request, CancellationToken ct)
    {
        var payload = new[]
        {
            new
            {
                organization_id = organizationId,
                instance_id = request.InstanceId,
                instance_name = request.InstanceName,
                description = request.Description,
                api_key_hash = string.IsNullOrWhiteSpace(request.ApiKey) ? null : ComputeSha256(request.ApiKey.Trim()),
                is_active = true,
                last_seen_utc = DateTime.UtcNow
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "central_instances?on_conflict=instance_id");
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = JsonContent.Create(payload);

        using var resp = await _httpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var rows = await resp.Content.ReadFromJsonAsync<List<SupabaseCentral>>(JsonOptions, ct) ?? new List<SupabaseCentral>();
        return rows[0];
    }

    private async Task<Guid?> GetCentralDbIdAsync(Guid instanceId, CancellationToken ct)
    {
        var url = $"central_instances?instance_id=eq.{instanceId}&select=id,instance_id,instance_name&limit=1";
        var rows = await _httpClient.GetFromJsonAsync<List<SupabaseCentral>>(url, JsonOptions, ct) ?? new List<SupabaseCentral>();
        return rows.FirstOrDefault()?.Id;
    }

    private Task<HttpResponseMessage> PostAsync(string resource, object payload, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, resource);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(payload);
        return _httpClient.SendAsync(req, ct);
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private sealed class SupabaseOrganization
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";
    }

    private sealed class SupabaseCentral
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("instance_id")]
        public Guid InstanceId { get; set; }
        [JsonPropertyName("instance_name")]
        public string InstanceName { get; set; } = "";
    }
}

public class RegisterCentralResult
{
    public Guid OrganizationId { get; set; }
    public Guid CentralId { get; set; }
    public Guid InstanceId { get; set; }
    public string InstanceName { get; set; } = "";
}

public class AlertSyncResult
{
    public int InsertedOrUpdated { get; set; }
    public int Failed { get; set; }
}

public class UserCentralSummary
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
    [JsonPropertyName("central_id")]
    public Guid CentralId { get; set; }
    [JsonPropertyName("instance_id")]
    public Guid InstanceId { get; set; }
    [JsonPropertyName("instance_name")]
    public string InstanceName { get; set; } = "";
    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [JsonPropertyName("last_seen_utc")]
    public DateTime? LastSeenUtc { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public class AppUserProfile
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}

public class CentralSnapshotSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("snapshot_timestamp_utc")]
    public DateTime SnapshotTimestampUtc { get; set; }
    [JsonPropertyName("hosts_total")]
    public int HostsTotal { get; set; }
    [JsonPropertyName("hosts_active")]
    public int HostsActive { get; set; }
    [JsonPropertyName("alerts_open")]
    public int AlertsOpen { get; set; }
    [JsonPropertyName("critical_alerts")]
    public int CriticalAlerts { get; set; }
    [JsonPropertyName("warning_alerts")]
    public int WarningAlerts { get; set; }
    [JsonPropertyName("summary_json")]
    public JsonElement? SummaryJson { get; set; }
}

public class CloudAlertSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("central_instance_id")]
    public Guid CentralInstanceId { get; set; }
    [JsonPropertyName("source_alert_id")]
    public Guid SourceAlertId { get; set; }
    [JsonPropertyName("host_id")]
    public Guid? HostId { get; set; }
    [JsonPropertyName("host_name")]
    public string? HostName { get; set; }
    [JsonPropertyName("metric_key")]
    public string MetricKey { get; set; } = "";
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("trigger_value")]
    public double? TriggerValue { get; set; }
    [JsonPropertyName("threshold")]
    public double? Threshold { get; set; }
    [JsonPropertyName("labels_json")]
    public JsonElement? LabelsJson { get; set; }
    [JsonPropertyName("last_synced_at_utc")]
    public DateTime LastSyncedAtUtc { get; set; }
    [JsonPropertyName("opened_at_utc")]
    public DateTime OpenedAtUtc { get; set; }
    [JsonPropertyName("resolved_at_utc")]
    public DateTime? ResolvedAtUtc { get; set; }
}

public class MobileDeviceTokenRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";
    [JsonPropertyName("device_token")]
    public string DeviceToken { get; set; } = "";
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
    [JsonPropertyName("last_seen_utc")]
    public DateTime LastSeenUtc { get; set; }
}

public class CentralUserAccessSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("central_instance_id")]
    public Guid CentralInstanceId { get; set; }
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
    [JsonPropertyName("app_users")]
    public AppUserProfile? AppUser { get; set; }
}

public class CentralUserAccessFlat
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("central_instance_id")]
    public Guid CentralInstanceId { get; set; }
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
