using CentralMonitoring.Shared.DTOs.SnmpTargets;
using Microsoft.AspNetCore.Mvc;

namespace CentralMonitoring.Api.Controllers;

[ApiController]
[Route("api/v1/metrics/catalog")]
public class MetricsCatalogController : ControllerBase
{
    private readonly IConfiguration _config;

    public MetricsCatalogController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public ActionResult<List<SnmpMetricDto>> Get()
    {
        var list = _config.GetSection("Snmp:Metrics").Get<List<SnmpMetricDto>>() ?? new();
        return Ok(list);
    }
}
