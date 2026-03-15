using CentralMonitoring.Domain.Entities;
using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Shared.DTOs.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/rules")]
public class RulesController : ControllerBase
{
    private static readonly HashSet<string> AllowedOps = new(new[] { ">", ">=", "<", "<=", "==", "!=" }, StringComparer.Ordinal);

    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public RulesController(MonitoringDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost]
    public async Task<ActionResult<RuleResponse>> Create([FromBody] RuleCreateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.MetricKey))
            return BadRequest("MetricKey is required.");
        if (string.IsNullOrWhiteSpace(req.Operator) || !AllowedOps.Contains(req.Operator))
            return BadRequest("Operator must be one of >, >=, <, <=, ==, !=.");
        if (req.WindowMinutes <= 0) return BadRequest("WindowMinutes must be > 0.");
        if (string.IsNullOrWhiteSpace(req.Severity)) return BadRequest("Severity is required.");

        var rule = new Rule
        {
            MetricKey = req.MetricKey.Trim(),
            Operator = req.Operator.Trim(),
            Threshold = req.Threshold,
            WindowMinutes = req.WindowMinutes,
            Severity = req.Severity.Trim(),
            HostId = req.HostId == Guid.Empty ? null : req.HostId,
            SnmpIp = string.IsNullOrWhiteSpace(req.SnmpIp) ? null : req.SnmpIp.Trim(),
            LabelContains = string.IsNullOrWhiteSpace(req.LabelContains) ? null : req.LabelContains.Trim(),
            Enabled = req.Enabled,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Rules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, ToResponse(rule));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuleResponse>>> GetAll(CancellationToken ct)
    {
        var rows = await _db.Rules
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => ToResponse(r))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RuleResponse>> GetById(Guid id, CancellationToken ct)
    {
        var r = await _db.Rules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        return Ok(ToResponse(r));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RuleUpdateRequest req, CancellationToken ct)
    {
        var r = await _db.Rules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();

        if (req.Operator is not null)
        {
            if (!AllowedOps.Contains(req.Operator))
                return BadRequest("Invalid operator.");
            r.Operator = req.Operator;
        }

        if (req.Threshold.HasValue) r.Threshold = req.Threshold.Value;
        if (req.WindowMinutes.HasValue)
        {
            if (req.WindowMinutes.Value <= 0) return BadRequest("WindowMinutes must be > 0.");
            r.WindowMinutes = req.WindowMinutes.Value;
        }
        if (!string.IsNullOrWhiteSpace(req.Severity)) r.Severity = req.Severity.Trim();
        r.HostId = req.HostId == Guid.Empty ? null : req.HostId ?? r.HostId;
        if (req.SnmpIp != null) r.SnmpIp = string.IsNullOrWhiteSpace(req.SnmpIp) ? null : req.SnmpIp.Trim();
        if (req.LabelContains != null) r.LabelContains = string.IsNullOrWhiteSpace(req.LabelContains) ? null : req.LabelContains.Trim();
        if (req.Enabled.HasValue) r.Enabled = req.Enabled.Value;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("bootstrap/agent/{hostId:guid}")]
    public async Task<ActionResult<object>> BootstrapAgentRules(Guid hostId, CancellationToken ct)
    {
        var host = await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hostId, ct);
        if (host is null) return NotFound("Host not found.");

        var generated = AgentAutoRulesFactory.Build(_config, hostId, host.Type);
        var inserted = 0;
        var skipped = 0;

        foreach (var rule in generated)
        {
            var exists = await _db.Rules.AnyAsync(r =>
                r.HostId == hostId &&
                r.MetricKey == rule.MetricKey &&
                (r.LabelContains ?? "") == (rule.LabelContains ?? ""), ct);

            if (exists)
            {
                skipped += 1;
                continue;
            }

            _db.Rules.Add(rule);
            inserted += 1;
        }

        if (inserted > 0)
            await _db.SaveChangesAsync(ct);

        return Ok(new { hostId, inserted, skipped });
    }

    private static RuleResponse ToResponse(Rule r) => new()
    {
        Id = r.Id,
        MetricKey = r.MetricKey,
        Operator = r.Operator,
        Threshold = r.Threshold,
        WindowMinutes = r.WindowMinutes,
        Severity = r.Severity,
        HostId = r.HostId,
        SnmpIp = r.SnmpIp,
        LabelContains = r.LabelContains,
        Enabled = r.Enabled,
        CreatedAtUtc = r.CreatedAtUtc
    };
}
