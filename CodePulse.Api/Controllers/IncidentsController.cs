using CodePulse.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService    _incidentService;
    private readonly ICorrelationService _correlationService;

    public IncidentsController(IIncidentService incidentService, ICorrelationService correlationService)
    {
        _incidentService    = incidentService;
        _correlationService = correlationService;
    }

    // GET /api/incidents
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var incidents = await _incidentService.GetAllAsync();
        return Ok(incidents);
    }

    // GET /api/incidents/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var incident = await _incidentService.GetByIdAsync(id);

        if (incident is null)
            return NotFound();

        return Ok(incident);
    }

    // GET /api/incidents/{id}/correlations
    [HttpGet("{id}/correlations")]
    public async Task<IActionResult> GetCorrelations(Guid id)
    {
        var incident = await _incidentService.GetByIdAsync(id);

        if (incident is null)
            return NotFound();

        var correlations = await _correlationService.GetForIncidentAsync(id);
        return Ok(correlations);
    }
}
