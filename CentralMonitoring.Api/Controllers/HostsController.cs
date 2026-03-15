using CentralMonitoring.Infrastructure.Persistence;
using CentralMonitoring.Shared.DTOs.Hosts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/hosts")]
public class HostsController : ControllerBase
{
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public HostsController(MonitoringDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost]
    public async Task<ActionResult<HostResponse>> Create([FromBody] HostCreateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (string.IsNullOrWhiteSpace(req.IpAddress))
            return BadRequest("IpAddress is required.");

        var host = new CentralMonitoring.Domain.Entities.Host
        {
            Name = req.Name.Trim(),
            IpAddress = req.IpAddress.Trim(),
            Type = req.Type.ToString(),
            Tags = string.IsNullOrWhiteSpace(req.Tags) ? null : req.Tags.Trim(),
            IsActive = req.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Hosts.Add(host);
        await _db.SaveChangesAsync(ct);
        await EnsureAgentBaseRules(host.Id, host.Type, ct);

        var resp = new HostResponse
        {
            Id = host.Id,
            Name = host.Name,
            IpAddress = host.IpAddress,
            Type = host.Type,
            Tags = host.Tags,
            IsActive = host.IsActive,
            CreatedAtUtc = host.CreatedAtUtc
        };

        return CreatedAtAction(nameof(GetById), new { id = host.Id }, resp);
    }

    [HttpGet]
    public async Task<ActionResult<List<HostResponse>>> GetAll(CancellationToken ct)
    {
        var hosts = await _db.Hosts
            .OrderBy(h => h.Name)
            .Select(h => new HostResponse
            {
                Id = h.Id,
                Name = h.Name,
                IpAddress = h.IpAddress,
                Type = h.Type,
                Tags = h.Tags,
                IsActive = h.IsActive,
                CreatedAtUtc = h.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(hosts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HostResponse>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (host is null) return NotFound();

        return Ok(new HostResponse
        {
            Id = host.Id,
            Name = host.Name,
            IpAddress = host.IpAddress,
            Type = host.Type,
            Tags = host.Tags,
            IsActive = host.IsActive,
            CreatedAtUtc = host.CreatedAtUtc
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] HostUpdateRequest req, CancellationToken ct)
    {
        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (host is null) return NotFound();

        if (req.Name is not null)
        {
            var name = req.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name cannot be empty.");
            host.Name = name;
        }

        if (req.IpAddress is not null)
        {
            var ip = req.IpAddress.Trim();
            if (string.IsNullOrWhiteSpace(ip)) return BadRequest("IpAddress cannot be empty.");
            host.IpAddress = ip;
        }

        if (req.Type.HasValue)
            host.Type = req.Type.Value.ToString();

        if (req.Tags is not null)
            host.Tags = string.IsNullOrWhiteSpace(req.Tags) ? null : req.Tags.Trim();

        if (req.IsActive.HasValue)
            host.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (host is null) return NotFound();

        // Rules keep a logical HostId reference (no FK), so clean them explicitly.
        var hostRules = await _db.Rules.Where(r => r.HostId == id).ToListAsync(ct);
        if (hostRules.Count > 0)
            _db.Rules.RemoveRange(hostRules);

        _db.Hosts.Remove(host);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task EnsureAgentBaseRules(Guid hostId, string? hostType, CancellationToken ct)
    {
        var generated = AgentAutoRulesFactory.Build(_config, hostId, hostType);
        if (generated.Count == 0) return;

        foreach (var rule in generated)
        {
            var exists = await _db.Rules.AnyAsync(r =>
                r.HostId == hostId &&
                r.MetricKey == rule.MetricKey &&
                (r.LabelContains ?? "") == (rule.LabelContains ?? ""), ct);

            if (!exists)
                _db.Rules.Add(rule);
        }

        await _db.SaveChangesAsync(ct);
    }
}
