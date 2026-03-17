using System.Security.Claims;
using System.Text.Json;
using CentralMonitoring.CloudApi.DTOs.Admin;
using CentralMonitoring.CloudApi.DTOs.Alerts;
using CentralMonitoring.CloudApi.DTOs.Auth;
using CentralMonitoring.CloudApi.DTOs.Centrals;
using CentralMonitoring.CloudApi.DTOs.Mobile;
using CentralMonitoring.CloudApi.Options;
using CentralMonitoring.CloudApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection("Supabase"));

var supabaseOptions = builder.Configuration.GetSection("Supabase").Get<SupabaseOptions>() ?? new SupabaseOptions();
var cloudApiBaseUrl = builder.Configuration["CloudApi:PublicBaseUrl"]?.TrimEnd('/');
var authLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("CloudApi.Auth");
IdentityModelEventSource.ShowPII = true;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = supabaseOptions.JwtIssuer;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseOptions.JwtIssuer,
            ValidateAudience = true,
            ValidAudiences = new[] { "authenticated", "anon", "service_role" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "email",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                authLogger.LogError(context.Exception, "JWT authentication failed.");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (!string.IsNullOrWhiteSpace(context.ErrorDescription))
                    authLogger.LogWarning("JWT challenge: {Error} - {Description}", context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient<SupabaseRestClient>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "CentralMonitoring.CloudApi",
    status = "running",
    environment = app.Environment.EnvironmentName
}));

app.MapHealthChecks("/health");

app.MapPost("/api/v1/auth/login", (LoginRequest? request, SupabaseRestClient supabase) =>
{
    var loginRequest = request ?? new LoginRequest();
    var loginUrl = supabase.BuildLoginUrl(loginRequest, cloudApiBaseUrl);

    return Results.Ok(new
    {
        provider = string.IsNullOrWhiteSpace(loginRequest.Provider) ? "google" : loginRequest.Provider,
        loginUrl
    });
});

app.MapGet("/api/v1/me", async (ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var profile = await supabase.GetAppUserAsync(userId.Value, ct);
    if (profile is null) return Results.NotFound(new { message = "User not found in app_users." });

    return Results.Ok(new
    {
        id = profile.Id,
        email = profile.Email,
        fullName = profile.FullName,
        isActive = profile.IsActive,
        createdAtUtc = profile.CreatedAtUtc
    });
}).RequireAuthorization();

app.MapGet("/api/v1/me/centrals", async (ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var centrals = await supabase.GetUserCentralsAsync(userId.Value, ct);
    return Results.Ok(centrals.Select(c => new
    {
        userId = c.UserId,
        email = c.Email,
        centralId = c.CentralId,
        instanceId = c.InstanceId,
        instanceName = c.InstanceName,
        organizationId = c.OrganizationId,
        role = c.Role,
        lastSeenUtc = c.LastSeenUtc,
        isActive = c.IsActive
    }));
}).RequireAuthorization();

app.MapGet("/api/v1/admin/centrals/{instanceId:guid}/users", async (Guid instanceId, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var access = await supabase.GetUserCentralByInstanceAsync(userId.Value, instanceId, ct);
    if (access is null) return Results.NotFound(new { message = "Central not found for current user." });
    if (!IsAdminRole(access.Role)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var users = await supabase.GetCentralUsersAsync(instanceId, ct);
    return Results.Ok(users.Select(u => new
    {
        id = u.Id,
        centralInstanceId = u.CentralInstanceId,
        userId = u.UserId,
        role = u.Role,
        isActive = u.IsActive,
        createdAtUtc = u.CreatedAtUtc,
        user = u.AppUser is null ? null : new
        {
            id = u.AppUser.Id,
            email = u.AppUser.Email,
            fullName = u.AppUser.FullName,
            isActive = u.AppUser.IsActive,
            createdAtUtc = u.AppUser.CreatedAtUtc
        }
    }));
}).RequireAuthorization();

app.MapPost("/api/v1/admin/centrals/{instanceId:guid}/users", async (Guid instanceId, AssignCentralUserRequest request, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");
    if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest("email is required.");
    if (!IsSupportedCentralRole(request.Role)) return Results.BadRequest("role must be owner, admin, operator or readonly.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var access = await supabase.GetUserCentralByInstanceAsync(userId.Value, instanceId, ct);
    if (access is null) return Results.NotFound(new { message = "Central not found for current user." });
    if (!IsAdminRole(access.Role)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var assigned = await supabase.AssignUserToCentralAsync(instanceId, request, ct);
    return assigned is null
        ? Results.NotFound(new { message = "Target user not found in cloud. User must login first." })
        : Results.Ok(new
        {
            id = assigned.Id,
            centralInstanceId = assigned.CentralInstanceId,
            userId = assigned.UserId,
            role = assigned.Role,
            isActive = assigned.IsActive,
            createdAtUtc = assigned.CreatedAtUtc,
            user = assigned.AppUser is null ? null : new
            {
                id = assigned.AppUser.Id,
                email = assigned.AppUser.Email,
                fullName = assigned.AppUser.FullName,
                isActive = assigned.AppUser.IsActive,
                createdAtUtc = assigned.AppUser.CreatedAtUtc
            }
        });
}).RequireAuthorization();

app.MapDelete("/api/v1/admin/centrals/{instanceId:guid}/users/{targetUserId:guid}", async (Guid instanceId, Guid targetUserId, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");
    if (targetUserId == Guid.Empty) return Results.BadRequest("targetUserId is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var access = await supabase.GetUserCentralByInstanceAsync(userId.Value, instanceId, ct);
    if (access is null) return Results.NotFound(new { message = "Central not found for current user." });
    if (!IsAdminRole(access.Role)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var removed = await supabase.RemoveUserFromCentralAsync(instanceId, targetUserId, ct);
    return removed
        ? Results.Ok(new { instanceId, targetUserId, removed = true })
        : Results.NotFound(new { instanceId, targetUserId, removed = false });
}).RequireAuthorization();

app.MapPost("/api/v1/centrals/register", async (RegisterCentralRequest request, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (request.InstanceId == Guid.Empty) return Results.BadRequest("InstanceId is required.");
    if (string.IsNullOrWhiteSpace(request.InstanceName)) return Results.BadRequest("InstanceName is required.");
    if (string.IsNullOrWhiteSpace(request.OrganizationName)) return Results.BadRequest("OrganizationName is required.");
    if (string.IsNullOrWhiteSpace(request.OrganizationSlug)) return Results.BadRequest("OrganizationSlug is required.");

    var result = await supabase.RegisterCentralAsync(request, ct);
    return Results.Ok(result);
});

app.MapPost("/api/v1/centrals/{instanceId:guid}/heartbeat", async (Guid instanceId, HeartbeatRequest? request, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");

    var ok = await supabase.HeartbeatAsync(instanceId, request?.LastSeenUtc ?? DateTime.UtcNow, ct);
    return ok ? Results.Ok(new { instanceId, updated = true }) : Results.NotFound(new { instanceId, updated = false });
});

app.MapPost("/api/v1/centrals/{instanceId:guid}/snapshots", async (Guid instanceId, SnapshotSyncRequest request, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");

    var ok = await supabase.InsertSnapshotAsync(instanceId, request, ct);
    return ok ? Results.Ok(new { instanceId, inserted = true }) : Results.NotFound(new { instanceId, inserted = false });
});

app.MapPost("/api/v1/centrals/{instanceId:guid}/alerts/sync", async (Guid instanceId, AlertSyncRequest request, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");
    var result = await supabase.UpsertAlertsAsync(instanceId, request, ct);
    return Results.Ok(new
    {
        instanceId,
        insertedOrUpdated = result.InsertedOrUpdated,
        failed = result.Failed
    });
});

app.MapGet("/api/v1/mobile/centrals", async (ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var centrals = await supabase.GetUserCentralsAsync(userId.Value, ct);
    return Results.Ok(centrals.Select(c => new
    {
        userId = c.UserId,
        email = c.Email,
        centralId = c.CentralId,
        instanceId = c.InstanceId,
        instanceName = c.InstanceName,
        organizationId = c.OrganizationId,
        role = c.Role,
        lastSeenUtc = c.LastSeenUtc,
        isActive = c.IsActive
    }));
}).RequireAuthorization();

app.MapGet("/api/v1/mobile/centrals/{instanceId:guid}/summary", async (Guid instanceId, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (instanceId == Guid.Empty) return Results.BadRequest("instanceId is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var central = await supabase.GetUserCentralByInstanceAsync(userId.Value, instanceId, ct);
    if (central is null) return Results.NotFound(new { message = "Central not found for current user." });

    var summary = await supabase.GetLatestSummaryAsync(instanceId, ct);
    return summary is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            id = summary.Id,
            snapshotTimestampUtc = summary.SnapshotTimestampUtc,
            hostsTotal = summary.HostsTotal,
            hostsActive = summary.HostsActive,
            alertsOpen = summary.AlertsOpen,
            criticalAlerts = summary.CriticalAlerts,
            warningAlerts = summary.WarningAlerts,
            summaryJson = summary.SummaryJson
        });
}).RequireAuthorization();

app.MapGet("/api/v1/mobile/alerts", async (ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var alerts = await supabase.GetUserCurrentAlertsAsync(userId.Value, ct);
    return Results.Ok(alerts.Select(a => new
    {
        id = a.Id,
        centralInstanceId = a.CentralInstanceId,
        sourceAlertId = a.SourceAlertId,
        hostId = a.HostId,
        hostName = a.HostName,
        metricKey = a.MetricKey,
        metricDisplayName = BuildMetricDisplayName(a.MetricKey),
        sourceType = BuildSourceType(a),
        severity = a.Severity,
        status = a.Status,
        triggerValue = a.TriggerValue,
        threshold = a.Threshold,
        labelsJson = a.LabelsJson,
        reason = BuildAlertReason(a),
        openedAtUtc = a.OpenedAtUtc,
        resolvedAtUtc = a.ResolvedAtUtc
    }));
}).RequireAuthorization();

app.MapPost("/api/v1/mobile/alerts/{cloudAlertId:guid}/ack", async (Guid cloudAlertId, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (cloudAlertId == Guid.Empty) return Results.BadRequest("cloudAlertId is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var updated = await supabase.SetAlertStatusAsync(userId.Value, cloudAlertId, "Acked", ct);
    return updated is null
        ? Results.NotFound(new { message = "Alert not found for current user." })
        : Results.Ok(new
        {
            id = updated.Id,
            centralInstanceId = updated.CentralInstanceId,
            sourceAlertId = updated.SourceAlertId,
            hostId = updated.HostId,
            hostName = updated.HostName,
            metricKey = updated.MetricKey,
            metricDisplayName = BuildMetricDisplayName(updated.MetricKey),
            sourceType = BuildSourceType(updated),
            severity = updated.Severity,
            status = updated.Status,
            triggerValue = updated.TriggerValue,
            threshold = updated.Threshold,
            labelsJson = updated.LabelsJson,
            reason = BuildAlertReason(updated),
            openedAtUtc = updated.OpenedAtUtc,
            resolvedAtUtc = updated.ResolvedAtUtc
        });
}).RequireAuthorization();

app.MapPost("/api/v1/mobile/alerts/{cloudAlertId:guid}/resolve", async (Guid cloudAlertId, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (cloudAlertId == Guid.Empty) return Results.BadRequest("cloudAlertId is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var updated = await supabase.SetAlertStatusAsync(userId.Value, cloudAlertId, "Resolved", ct);
    return updated is null
        ? Results.NotFound(new { message = "Alert not found for current user." })
        : Results.Ok(new
        {
            id = updated.Id,
            centralInstanceId = updated.CentralInstanceId,
            sourceAlertId = updated.SourceAlertId,
            hostId = updated.HostId,
            hostName = updated.HostName,
            metricKey = updated.MetricKey,
            metricDisplayName = BuildMetricDisplayName(updated.MetricKey),
            sourceType = BuildSourceType(updated),
            severity = updated.Severity,
            status = updated.Status,
            triggerValue = updated.TriggerValue,
            threshold = updated.Threshold,
            labelsJson = updated.LabelsJson,
            reason = BuildAlertReason(updated),
            openedAtUtc = updated.OpenedAtUtc,
            resolvedAtUtc = updated.ResolvedAtUtc
        });
}).RequireAuthorization();

app.MapPost("/api/v1/mobile/device-tokens", async (RegisterDeviceTokenRequest request, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.Platform)) return Results.BadRequest("platform is required.");
    if (string.IsNullOrWhiteSpace(request.DeviceToken)) return Results.BadRequest("deviceToken is required.");

    var normalizedPlatform = request.Platform.Trim().ToLowerInvariant();
    if (normalizedPlatform is not ("android" or "ios"))
        return Results.BadRequest("platform must be android or ios.");

    var record = await supabase.RegisterDeviceTokenAsync(userId.Value, request, ct);
    return Results.Ok(new
    {
        id = record.Id,
        userId = record.UserId,
        platform = record.Platform,
        deviceToken = record.DeviceToken,
        isActive = record.IsActive,
        createdAtUtc = record.CreatedAtUtc,
        lastSeenUtc = record.LastSeenUtc
    });
}).RequireAuthorization();

app.MapDelete("/api/v1/mobile/device-tokens/{id:guid}", async (Guid id, ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    if (id == Guid.Empty) return Results.BadRequest("id is required.");

    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var deleted = await supabase.DeleteDeviceTokenAsync(userId.Value, id, ct);
    return deleted ? Results.Ok(new { id, deleted = true }) : Results.NotFound(new { id, deleted = false });
}).RequireAuthorization();

app.MapGet("/api/v1/mobile/dashboard", async (ClaimsPrincipal user, SupabaseRestClient supabase, CancellationToken ct) =>
{
    var userId = GetRequiredUserId(user);
    if (userId is null) return Results.Unauthorized();

    var profile = await supabase.GetAppUserAsync(userId.Value, ct);
    if (profile is null) return Results.NotFound(new { message = "User not found in app_users." });

    var centrals = await supabase.GetUserCentralsAsync(userId.Value, ct);
    var alerts = await supabase.GetUserCurrentAlertsAsync(userId.Value, ct);

    var centralCards = new List<object>(centrals.Count);
    foreach (var central in centrals)
    {
        var latestSummary = await supabase.GetLatestSummaryAsync(central.InstanceId, ct);
        centralCards.Add(new
        {
            centralId = central.CentralId,
            instanceId = central.InstanceId,
            instanceName = central.InstanceName,
            organizationId = central.OrganizationId,
            role = central.Role,
            lastSeenUtc = central.LastSeenUtc,
            isActive = central.IsActive,
            hostsTotal = latestSummary?.HostsTotal ?? 0,
            hostsActive = latestSummary?.HostsActive ?? 0,
            alertsOpen = latestSummary?.AlertsOpen ?? 0,
            criticalAlerts = latestSummary?.CriticalAlerts ?? 0,
            warningAlerts = latestSummary?.WarningAlerts ?? 0,
            snapshotTimestampUtc = latestSummary?.SnapshotTimestampUtc
        });
    }

    return Results.Ok(new
    {
        user = new
        {
            id = profile.Id,
            email = profile.Email,
            fullName = profile.FullName,
            isActive = profile.IsActive
        },
        centrals = centralCards,
        openAlertsTop = alerts
            .Where(a => string.Equals(a.Status, "Open", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Status, "Acked", StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(a => new
            {
                id = a.Id,
                centralInstanceId = a.CentralInstanceId,
                sourceAlertId = a.SourceAlertId,
                hostId = a.HostId,
                hostName = a.HostName,
                metricKey = a.MetricKey,
                metricDisplayName = BuildMetricDisplayName(a.MetricKey),
                sourceType = BuildSourceType(a),
                severity = a.Severity,
                status = a.Status,
                triggerValue = a.TriggerValue,
                threshold = a.Threshold,
                labelsJson = a.LabelsJson,
                reason = BuildAlertReason(a),
                openedAtUtc = a.OpenedAtUtc,
                resolvedAtUtc = a.ResolvedAtUtc
            })
    });
}).RequireAuthorization();

app.Run();

static Guid? GetRequiredUserId(ClaimsPrincipal user)
{
    var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    return Guid.TryParse(raw, out var id) ? id : null;
}

static bool IsAdminRole(string? role) =>
    role is not null &&
    (role.Equals("owner", StringComparison.OrdinalIgnoreCase) ||
     role.Equals("admin", StringComparison.OrdinalIgnoreCase));

static bool IsSupportedCentralRole(string? role) =>
    role is not null &&
    (role.Equals("owner", StringComparison.OrdinalIgnoreCase) ||
     role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
     role.Equals("operator", StringComparison.OrdinalIgnoreCase) ||
     role.Equals("readonly", StringComparison.OrdinalIgnoreCase));

static string BuildSourceType(CloudAlertSummary alert)
{
    if (TryGetLabelString(alert, "source_type", out var sourceType) && !string.IsNullOrWhiteSpace(sourceType))
        return sourceType!;

    if (alert.MetricKey.StartsWith("snmp_", StringComparison.OrdinalIgnoreCase) || TryGetLabelString(alert, "snmp_ip", out _))
        return "snmp";

    return "agent";
}

static string BuildMetricDisplayName(string metricKey) =>
    metricKey switch
    {
        "cpu_usage_pct" => "CPU usage",
        "agent_cpu_usage_pct" => "Agent CPU usage",
        "mem_used_pct" => "Memory usage",
        "disk_used_pct" => "Disk usage",
        "service_up" => "Critical service state",
        "snmp_poll_failure" => "SNMP poll failure",
        _ when metricKey.StartsWith("snmp_ifOperStatus_", StringComparison.OrdinalIgnoreCase) => "SNMP interface status",
        _ when metricKey.StartsWith("snmp_ifInErrors_", StringComparison.OrdinalIgnoreCase) => "SNMP input errors",
        _ when metricKey.StartsWith("snmp_ifOutErrors_", StringComparison.OrdinalIgnoreCase) => "SNMP output errors",
        _ when metricKey.StartsWith("net_rx_errors", StringComparison.OrdinalIgnoreCase) => "Network RX errors",
        _ when metricKey.StartsWith("net_tx_errors", StringComparison.OrdinalIgnoreCase) => "Network TX errors",
        _ => metricKey
    };

static string BuildAlertReason(CloudAlertSummary alert)
{
    var trigger = FormatNumber(alert.TriggerValue);
    var threshold = FormatNumber(alert.Threshold);
    var hostName = string.IsNullOrWhiteSpace(alert.HostName) ? "device" : alert.HostName;

    return alert.MetricKey switch
    {
        "mem_used_pct" => $"Memory usage {trigger}% exceeds threshold {threshold}% on {hostName}.",
        "cpu_usage_pct" => $"CPU usage {trigger}% exceeds threshold {threshold}% on {hostName}.",
        "agent_cpu_usage_pct" => $"Agent CPU usage {trigger}% exceeds threshold {threshold}% on {hostName}.",
        "disk_used_pct" => $"Disk {GetLabelOrDefault(alert, "drive", "unknown")} usage {trigger}% exceeds threshold {threshold}% on {hostName}.",
        "service_up" => $"Critical service '{GetLabelOrDefault(alert, "service", "unknown")}' is reported as down on {hostName}.",
        "snmp_poll_failure" => $"SNMP polling failed for {GetLabelOrDefault(alert, "snmp_ip", hostName)}. {GetLabelOrDefault(alert, "failure_reason", "No extra reason reported.")} Consecutive failures: {trigger}.",
        _ when alert.MetricKey.StartsWith("snmp_ifOperStatus_", StringComparison.OrdinalIgnoreCase)
            => $"SNMP interface {GetLabelOrDefault(alert, "if_index", GetMetricSuffix(alert.MetricKey))} reports status {trigger}; expected {threshold} on {hostName}.",
        _ when alert.MetricKey.StartsWith("snmp_ifInErrors_", StringComparison.OrdinalIgnoreCase)
            => $"SNMP interface {GetLabelOrDefault(alert, "if_index", GetMetricSuffix(alert.MetricKey))} has input errors ({trigger}) on {hostName}.",
        _ when alert.MetricKey.StartsWith("snmp_ifOutErrors_", StringComparison.OrdinalIgnoreCase)
            => $"SNMP interface {GetLabelOrDefault(alert, "if_index", GetMetricSuffix(alert.MetricKey))} has output errors ({trigger}) on {hostName}.",
        _ when alert.MetricKey.StartsWith("net_rx_errors", StringComparison.OrdinalIgnoreCase)
            => $"Network interface '{GetLabelOrDefault(alert, "iface", "unknown")}' reports RX errors ({trigger}) on {hostName}.",
        _ when alert.MetricKey.StartsWith("net_tx_errors", StringComparison.OrdinalIgnoreCase)
            => $"Network interface '{GetLabelOrDefault(alert, "iface", "unknown")}' reports TX errors ({trigger}) on {hostName}.",
        _ => $"Metric '{BuildMetricDisplayName(alert.MetricKey)}' triggered on {hostName}. Value {trigger}, threshold {threshold}."
    };
}

static string GetMetricSuffix(string metricKey)
{
    var index = metricKey.LastIndexOf('_');
    return index >= 0 && index < metricKey.Length - 1 ? metricKey[(index + 1)..] : metricKey;
}

static string FormatNumber(double? value) =>
    value.HasValue ? value.Value.ToString("0.##") : "n/a";

static string GetLabelOrDefault(CloudAlertSummary alert, string propertyName, string fallback)
{
    return TryGetLabelString(alert, propertyName, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value!
        : fallback;
}

static bool TryGetLabelString(CloudAlertSummary alert, string propertyName, out string? value)
{
    value = null;
    if (alert.LabelsJson is null || alert.LabelsJson.Value.ValueKind != JsonValueKind.Object)
        return false;

    if (!alert.LabelsJson.Value.TryGetProperty(propertyName, out var property))
        return false;

    value = property.ValueKind switch
    {
        JsonValueKind.String => property.GetString(),
        JsonValueKind.Number => property.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => property.GetRawText()
    };
    return true;
}
