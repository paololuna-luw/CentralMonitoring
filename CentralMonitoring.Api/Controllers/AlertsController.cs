using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Shared.DTOs.Alerts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
public class AlertsController : ControllerBase
{
    private readonly MonitoringDbContext _db;

    public AlertsController(MonitoringDbContext db)
    {
        _db = db;
    }

    // GET /api/v1/alerts?resolved=false
    [HttpGet]
    public async Task<ActionResult<List<AlertResponse>>> Get([FromQuery] bool? resolved, CancellationToken ct)
    {
        var q = _db.AlertEvents.AsQueryable();

        if (resolved.HasValue)
            q = q.Where(a => a.IsResolved == resolved.Value);

        var rows = await q
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(500) // v1: límite sano
            .Select(a => new AlertResponse
            {
                Id = a.Id,
                HostId = a.HostId,
                MetricKey = a.MetricKey,
                TriggerValue = a.TriggerValue,
                LastTriggerValue = a.LastTriggerValue,
                Threshold = a.Threshold,
                Severity = a.Severity,
                CreatedAtUtc = a.CreatedAtUtc,
                LastTriggerAtUtc = a.LastTriggerAtUtc,
                Occurrences = a.Occurrences,
                IsResolved = a.IsResolved
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    // PATCH /api/v1/alerts/{id}/resolve
    [HttpPatch("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAlertRequest req, CancellationToken ct)
    {
        var alert = await _db.AlertEvents.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return NotFound();

        alert.IsResolved = req.IsResolved;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
