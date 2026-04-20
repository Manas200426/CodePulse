using CodePulse.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/anomalies")]
public class AnomaliesController : ControllerBase
{
    private readonly IAnomalyService _anomalyService;

    public AnomaliesController(IAnomalyService anomalyService)
    {
        _anomalyService = anomalyService;
    }

    // GET /api/anomalies
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var anomalies = await _anomalyService.GetAllAsync();
        return Ok(anomalies);
    }

    // GET /api/anomalies/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var anomaly = await _anomalyService.GetByIdAsync(id);

        if (anomaly is null)
            return NotFound();

        return Ok(anomaly);
    }
}
