using CodePulse.Contracts.Responses;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/dashboard/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var services = await _db.MonitoredServices
            .Where(x => x.IsActive)
            .ToListAsync();

        // Load latest check result per service (one query, not N queries)
        var latestChecks = await _db.HealthCheckResults
            .GroupBy(x => x.ServiceId)
            .Select(g => g.OrderByDescending(x => x.CheckedAtUtc).First())
            .ToListAsync();

        // Active incidents and anomalies per service
        var activeIncidentServiceIds = await _db.Incidents
            .Where(x => x.Status == IncidentStatus.Active)
            .Select(x => x.ServiceId)
            .ToHashSetAsync();

        var activeAnomalyServiceIds = await _db.Anomalies
            .Where(x => x.Status == AnomalyStatus.Active)
            .Select(x => x.ServiceId)
            .ToHashSetAsync();

        var latestCheckMap = latestChecks.ToDictionary(x => x.ServiceId);

        var serviceSummaries = services.Select(svc =>
        {
            var hasIncident = activeIncidentServiceIds.Contains(svc.Id);
            var hasAnomaly  = activeAnomalyServiceIds.Contains(svc.Id);
            latestCheckMap.TryGetValue(svc.Id, out var lastCheck);

            var healthStatus = hasIncident ? "Down"
                             : hasAnomaly  ? "Degraded"
                             : lastCheck   is null ? "Unknown"
                             : "Healthy";

            return new ServiceStatusSummary
            {
                Id                 = svc.Id,
                Name               = svc.Name,
                HealthStatus       = healthStatus,
                HasActiveIncident  = hasIncident,
                HasActiveAnomaly   = hasAnomaly,
                LatestResponseMs   = lastCheck?.ResponseTimeMs,
                LastCheckSuccess   = lastCheck?.IsSuccess,
                LastCheckedAtUtc   = lastCheck?.CheckedAtUtc
            };
        }).ToList();

        var summary = new DashboardSummaryResponse
        {
            TotalServices    = services.Count,
            ActiveIncidents  = activeIncidentServiceIds.Count,
            ActiveAnomalies  = activeAnomalyServiceIds.Count,
            DownServices     = serviceSummaries.Count(x => x.HealthStatus == "Down"),
            DegradedServices = serviceSummaries.Count(x => x.HealthStatus == "Degraded"),
            HealthyServices  = serviceSummaries.Count(x => x.HealthStatus == "Healthy"),
            Services         = serviceSummaries
        };

        return Ok(summary);
    }
}
