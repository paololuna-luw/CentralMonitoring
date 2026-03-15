using CentralMonitoring.Domain.Entities;
using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Shared.DTOs.SnmpTargets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/snmp/targets")]
public class SnmpTargetsController : ControllerBase
{
    private static readonly HashSet<string> AllowedVersions = new(StringComparer.OrdinalIgnoreCase)
    {
        "v1", "v2c" // v3 reservado
    };

    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public SnmpTargetsController(MonitoringDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost]
    public async Task<ActionResult<SnmpTargetResponse>> Create([FromBody] SnmpTargetCreateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Version) || !AllowedVersions.Contains(req.Version))
            return BadRequest("Version must be v1 or v2c (v3 pending).");

        if (string.Equals(req.Version, "v2c", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(req.Community))
            return BadRequest("Community is required for v2c.");

        CentralMonitoring.Domain.Entities.Host? hostFromRequest = null;
        if (req.HostId.HasValue)
        {
            hostFromRequest = await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.Id == req.HostId.Value, ct);
            if (hostFromRequest is null)
                return BadRequest("HostId not found.");
        }

        var requestedIp = req.IpAddress?.Trim();
        if (hostFromRequest is not null &&
            !string.IsNullOrWhiteSpace(requestedIp) &&
            !string.Equals(requestedIp, hostFromRequest.IpAddress, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("IpAddress does not match HostId.");
        }

        var resolvedIp = !string.IsNullOrWhiteSpace(requestedIp) ? requestedIp : hostFromRequest?.IpAddress;
        if (string.IsNullOrWhiteSpace(resolvedIp))
            return BadRequest("IpAddress or HostId is required.");

        var target = new SnmpTarget
        {
            IpAddress = resolvedIp,
            Version = req.Version.Trim(),
            Community = req.Community?.Trim(),
            Profile = string.IsNullOrWhiteSpace(req.Name)
                ? (string.IsNullOrWhiteSpace(req.Profile) ? null : req.Profile.Trim())
                : req.Name.Trim(),
            Tags = string.IsNullOrWhiteSpace(req.Tags) ? null : req.Tags.Trim(),
            MetricsJson = SerializeMetrics(req.Metrics),
            Enabled = req.Enabled,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SnmpTargets.Add(target);
        await _db.SaveChangesAsync(ct);

        // Auto-regla interfaz down si está habilitado en config
        var autoIfDown = _config.GetValue<bool?>("Snmp:AutoRules:InterfaceDown") ?? true;
        if (autoIfDown)
        {
            var rule = new Rule
            {
                MetricKey = "snmp_ifOperStatus_1",
                Operator = "!=",
                Threshold = 1,
                WindowMinutes = 2,
                Severity = "Critical",
                HostId = null,
                SnmpIp = target.IpAddress,
                Enabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Rules.Add(rule);
        }

        // Patrones adicionales desde config
        var patterns = _config.GetSection("Snmp:AutoRules:Patterns").Get<List<AutoRulePattern>>() ?? new();
        foreach (var t in patterns)
        {
            var metricKey = t.MetricKey;
            if (t.IfIndex.HasValue)
                metricKey = metricKey.Replace("{ifIndex}", t.IfIndex.Value.ToString());

            // evita duplicar si ya existe misma regla para mismo IP y metricKey
            var exists = await _db.Rules.AnyAsync(r =>
                r.MetricKey == metricKey &&
                r.SnmpIp == target.IpAddress, ct);
            if (exists) continue;

            var rule = new Rule
            {
                MetricKey = metricKey,
                Operator = string.IsNullOrWhiteSpace(t.Operator) ? ">" : t.Operator,
                Threshold = t.Threshold,
                WindowMinutes = t.WindowMinutes <= 0 ? 2 : t.WindowMinutes,
                Severity = string.IsNullOrWhiteSpace(t.Severity) ? "Warning" : t.Severity,
                SnmpIp = target.IpAddress,
                Enabled = t.Enabled,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Rules.Add(rule);
        }

        await _db.SaveChangesAsync(ct);

        var host = hostFromRequest ?? await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.IpAddress == target.IpAddress, ct);
        return CreatedAtAction(nameof(GetById), new { id = target.Id }, ToResponse(target, host));
    }

    [HttpGet]
    public async Task<ActionResult<List<SnmpTargetResponse>>> GetAll([FromQuery] string? ip, CancellationToken ct)
    {
        var targets = await _db.SnmpTargets
            .Where(t => string.IsNullOrWhiteSpace(ip) ||
                        t.IpAddress.ToLower().StartsWith(ip.ToLower()))
            .OrderBy(t => t.IpAddress)
            .ToListAsync(ct);

        var ips = targets.Select(t => t.IpAddress).Distinct().ToList();
        var hosts = await _db.Hosts.AsNoTracking()
            .Where(h => ips.Contains(h.IpAddress))
            .ToListAsync(ct);
        var hostByIp = hosts.GroupBy(h => h.IpAddress).ToDictionary(g => g.Key, g => g.First());

        var rows = targets
            .Select(t => ToResponse(t, hostByIp.TryGetValue(t.IpAddress, out var host) ? host : null))
            .ToList();
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SnmpTargetResponse>> GetById(Guid id, CancellationToken ct)
    {
        var t = await _db.SnmpTargets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        var host = await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.IpAddress == t.IpAddress, ct);
        return Ok(ToResponse(t, host));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SnmpTargetUpdateRequest req, CancellationToken ct)
    {
        var t = await _db.SnmpTargets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();

        if (req.HostId.HasValue)
        {
            var host = await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.Id == req.HostId.Value, ct);
            if (host is null) return BadRequest("HostId not found.");
            t.IpAddress = host.IpAddress;
        }

        if (!string.IsNullOrWhiteSpace(req.Community))
            t.Community = req.Community.Trim();

        if (req.Enabled.HasValue)
            t.Enabled = req.Enabled.Value;

        if (!string.IsNullOrWhiteSpace(req.Name))
            t.Profile = req.Name.Trim();
        else if (!string.IsNullOrWhiteSpace(req.Profile))
            t.Profile = req.Profile.Trim();

        if (!string.IsNullOrWhiteSpace(req.Tags))
            t.Tags = req.Tags.Trim();

        if (req.Metrics != null)
            t.MetricsJson = SerializeMetrics(req.Metrics);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/v1/snmp/targets/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var t = await _db.SnmpTargets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();

        _db.SnmpTargets.Remove(t);

        // Limpia reglas asociadas al mismo snmpIp (opcional)
        var rules = await _db.Rules.Where(r => r.SnmpIp == t.IpAddress).ToListAsync(ct);
        if (rules.Count > 0) _db.Rules.RemoveRange(rules);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // POST /api/v1/snmp/targets/{id}/metrics/enableAll
    [HttpPost("{id:guid}/metrics/enableAll")]
    public async Task<IActionResult> EnableAll(Guid id, CancellationToken ct)
    {
        return await ToggleAllMetrics(id, true, ct);
    }

    // POST /api/v1/snmp/targets/{id}/metrics/disableAll
    [HttpPost("{id:guid}/metrics/disableAll")]
    public async Task<IActionResult> DisableAll(Guid id, CancellationToken ct)
    {
        return await ToggleAllMetrics(id, false, ct);
    }

    private async Task<IActionResult> ToggleAllMetrics(Guid id, bool enabled, CancellationToken ct)
    {
        var t = await _db.SnmpTargets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();

        var targetMetrics = DeserializeMetrics(t.MetricsJson);
        if (targetMetrics == null || targetMetrics.Count == 0)
        {
            // si no hay override, usa lista global y la guarda como override
            targetMetrics = _config.GetSection("Snmp:Metrics").Get<List<SnmpMetricDto>>() ?? new();
        }

        foreach (var m in targetMetrics) m.Enabled = enabled;
        t.MetricsJson = SerializeMetrics(targetMetrics);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static SnmpTargetResponse ToResponse(SnmpTarget t, CentralMonitoring.Domain.Entities.Host? host = null) => new()
    {
        Id = t.Id,
        IpAddress = t.IpAddress,
        Version = t.Version,
        Community = t.Community,
        Name = t.Profile,
        Profile = t.Profile,
        HostId = host?.Id,
        HostName = host?.Name,
        Tags = t.Tags,
        Enabled = t.Enabled,
        CreatedAtUtc = t.CreatedAtUtc,
        ConsecutiveFailures = t.ConsecutiveFailures,
        LastSuccessUtc = t.LastSuccessUtc,
        LastFailureUtc = t.LastFailureUtc,
        Metrics = DeserializeMetrics(t.MetricsJson)
    };

    private static string? SerializeMetrics(List<SnmpMetricDto>? metrics)
    {
        if (metrics is null || metrics.Count == 0) return null;
        return JsonSerializer.Serialize(metrics);
    }

    private static List<SnmpMetricDto>? DeserializeMetrics(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<SnmpMetricDto>>(json);
        }
        catch
        {
            return null;
        }
    }
}

public class AutoRulePattern
{
    public string MetricKey { get; set; } = "";
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; }
    public int WindowMinutes { get; set; } = 2;
    public string Severity { get; set; } = "Warning";
    public int? IfIndex { get; set; }
    public bool Enabled { get; set; } = true;
}
