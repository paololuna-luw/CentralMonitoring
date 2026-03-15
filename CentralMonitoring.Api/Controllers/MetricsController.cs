using CentralMonitoring.Domain.Entities;
using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Shared.DTOs.Metrics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/metrics")]
public class MetricsController : ControllerBase
{
    private readonly MonitoringDbContext _db;

    public MetricsController(MonitoringDbContext db)
    {
        _db = db;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] MetricsIngestRequest req, CancellationToken ct)
    {
        if (req.HostId == Guid.Empty) return BadRequest("HostId is required.");
        if (req.Metrics is null || req.Metrics.Count == 0) return BadRequest("Metrics list is empty.");

        var hostExists = await _db.Hosts.AnyAsync(h => h.Id == req.HostId && h.IsActive, ct);
        if (!hostExists) return BadRequest("Host not found or inactive.");

        var ts = (req.TimestampUtc ?? DateTime.UtcNow);
        if (ts.Kind != DateTimeKind.Utc)
            ts = DateTime.SpecifyKind(ts, DateTimeKind.Utc);

        var samples = req.Metrics.Select(m => new MetricSample
        {
            HostId = req.HostId,
            MetricKey = m.Key.Trim(),
            Value = m.Value,
            LabelsJson = string.IsNullOrWhiteSpace(m.LabelsJson) ? null : m.LabelsJson,
            TimestampUtc = ts
        }).ToList();

        _db.MetricSamples.AddRange(samples);
        await _db.SaveChangesAsync(ct);

        return Ok(new { inserted = samples.Count });
    }

    [HttpGet("latest")]
    public async Task<ActionResult<List<MetricSampleResponse>>> Latest(
        [FromQuery] Guid? hostId,
        [FromQuery] string? snmpIp,
        [FromQuery] int? freshMinutes,
        CancellationToken ct)
    {
        if ((hostId is null || hostId == Guid.Empty) && string.IsNullOrWhiteSpace(snmpIp))
            return BadRequest("hostId or snmpIp is required.");
        var snmpLabel = BuildSnmpLabelFilter(snmpIp);

        // V1 estable: traemos un lote reciente y calculamos el "último por MetricKey" en memoria.
        var recent = await _db.MetricSamples
            .AsNoTracking()
            .Where(m =>
                (hostId.HasValue && hostId.Value != Guid.Empty && m.HostId == hostId.Value)
                || (!string.IsNullOrWhiteSpace(snmpLabel) && m.LabelsJson != null && m.LabelsJson.Contains(snmpLabel)))
            .OrderByDescending(m => m.TimestampUtc)
            .Take(5000)
            .ToListAsync(ct);

        var latest = recent
            .GroupBy(m => m.MetricKey)
            .Select(g => g.OrderByDescending(x => x.TimestampUtc).First())
            .OrderBy(x => x.MetricKey)
            .Select(r => new MetricSampleResponse
            {
                Id = r.Id,
                HostId = r.HostId,
                MetricKey = r.MetricKey,
                TimestampUtc = r.TimestampUtc,
                Value = r.Value,
                LabelsJson = r.LabelsJson
            })
            .ToList();

        if (freshMinutes.HasValue && freshMinutes.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-freshMinutes.Value);
            latest = latest.Where(m => m.TimestampUtc >= cutoff).ToList();
        }

        return Ok(latest);
    }


    [HttpGet("range")]
    public async Task<ActionResult<List<MetricSampleResponse>>> Range(
        [FromQuery] Guid? hostId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? metricKey,
        [FromQuery] string? snmpIp,
        CancellationToken ct)
    {
        if ((hostId is null || hostId == Guid.Empty) && string.IsNullOrWhiteSpace(snmpIp))
            return BadRequest("hostId or snmpIp is required.");
        if (to <= from) return BadRequest("to must be greater than from.");
        var snmpLabel = BuildSnmpLabelFilter(snmpIp);

        var q = _db.MetricSamples.AsQueryable()
            .Where(m =>
                ((hostId.HasValue && hostId.Value != Guid.Empty && m.HostId == hostId.Value) ||
                 (!string.IsNullOrWhiteSpace(snmpLabel) && m.LabelsJson != null && m.LabelsJson.Contains(snmpLabel)))
                && m.TimestampUtc >= from && m.TimestampUtc <= to);

        if (!string.IsNullOrWhiteSpace(metricKey))
            q = q.Where(m => m.MetricKey == metricKey.Trim());

        var rows = await q
            .OrderByDescending(m => m.TimestampUtc)
            .Take(5000) // v1: límite para no matar el server
            .Select(m => new MetricSampleResponse
            {
                Id = m.Id,
                HostId = m.HostId,
                MetricKey = m.MetricKey,
                TimestampUtc = m.TimestampUtc,
                Value = m.Value,
                LabelsJson = m.LabelsJson
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    private static string? BuildSnmpLabelFilter(string? snmpIp)
    {
        if (string.IsNullOrWhiteSpace(snmpIp)) return null;
        return $"\"snmp_ip\":\"{snmpIp.Trim()}\"";
    }
}
