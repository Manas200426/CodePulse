using CodePulse.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
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
}
