using CodePulse.Application.Services;
using CodePulse.Contracts.Requests;
using Microsoft.AspNetCore.Mvc;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoredServicesController : ControllerBase
{
    private readonly IMonitoredServiceService _service;

    public MonitoredServicesController(IMonitoredServiceService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMonitoredServiceRequest request)
    {
        var result = await _service.CreateAsync(request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateMonitoredServiceRequest request)
    {
        var success = await _service.UpdateAsync(id, request);

        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _service.DeleteAsync(id);

        if (!success)
            return NotFound();

        return NoContent();
    }

    // POST /api/monitoredservices/{id}/run-check
    // Triggers an immediate health check — useful for testing and the dashboard
    [HttpPost("{id}/run-check")]
    public async Task<IActionResult> RunCheck(Guid id)
    {
        var result = await _service.RunCheckAsync(id);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    // GET /api/monitoredservices/{id}/metrics
    // Returns aggregated stats: uptime %, average/p95/p99 latency, error rate
    [HttpGet("{id}/metrics")]
    public async Task<IActionResult> GetMetrics(Guid id)
    {
        var metrics = await _service.GetMetricsAsync(id);

        if (metrics is null)
            return NotFound();

        return Ok(metrics);
    }
}