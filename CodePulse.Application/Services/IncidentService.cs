using CodePulse.Contracts.Responses;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Application.Services;

public class IncidentService : IIncidentService
{
    private readonly AppDbContext _db;

    public IncidentService(AppDbContext db)
    {
        _db = db;
    }

    // ── Detection (called by HealthCheckWorker after every ping) ──────────────
    public async Task DetectAsync(Guid serviceId)
    {
        var lastChecks = await _db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(3)
            .ToListAsync();

        if (lastChecks.Count < 3)
            return;

        var existingIncident = await _db.Incidents
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.Status == IncidentStatus.Active);

        var allFailed  = lastChecks.All(x => !x.IsSuccess);
        var allSuccess = lastChecks.All(x => x.IsSuccess);

        // 🔴 CREATE incident — 3 consecutive failures
        if (allFailed && existingIncident == null)
        {
            var incident = new Incident
            {
                Id           = Guid.NewGuid(),
                ServiceId    = serviceId,
                Status       = IncidentStatus.Active,
                Severity     = IncidentSeverity.High,
                Reason       = "Service failed 3 consecutive health checks",
                FailureCount = 3,
                StartedAtUtc = DateTime.UtcNow
            };

            _db.Incidents.Add(incident);
            await _db.SaveChangesAsync();
        }

        // 🟢 RESOLVE incident — 3 consecutive successes
        if (allSuccess && existingIncident != null)
        {
            existingIncident.Status       = IncidentStatus.Resolved;
            existingIncident.ResolvedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
    }

    // ── Read operations (used by IncidentsController) ─────────────────────────
    public async Task<List<IncidentResponse>> GetAllAsync()
    {
        var incidents = await _db.Incidents
            .Include(x => x.Service)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync();

        return incidents.Select(MapToResponse).ToList();
    }

    public async Task<IncidentResponse?> GetByIdAsync(Guid id)
    {
        var incident = await _db.Incidents
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id);

        return incident is null ? null : MapToResponse(incident);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────
    private static IncidentResponse MapToResponse(Incident incident) => new()
    {
        Id           = incident.Id,
        ServiceId    = incident.ServiceId,
        ServiceName  = incident.Service?.Name ?? string.Empty,
        Status       = incident.Status.ToString(),
        Severity     = incident.Severity.ToString(),
        Reason       = incident.Reason,
        FailureCount = incident.FailureCount,
        StartedAtUtc = incident.StartedAtUtc,
        ResolvedAtUtc = incident.ResolvedAtUtc
    };
}
