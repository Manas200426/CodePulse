using CodePulse.Contracts.Responses;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Application.Services;

public class CorrelationService : ICorrelationService
{
    private readonly AppDbContext _db;

    // ── Tuning constants ──────────────────────────────────────────────────────
    // Max minutes between upstream and downstream incident starts
    // to still be considered a causal link.
    private const double MaxCorrelationWindowMinutes = 5.0;

    // Shared failure window: how many recent checks to compare failure patterns
    private const int SharedFailureWindow = 5;

    public CorrelationService(AppDbContext db)
    {
        _db = db;
    }

    // ── Detection — called by HealthCheckWorker ───────────────────────────────
    public async Task DetectAsync(Guid serviceId)
    {
        // 1. Does this service have an active incident right now?
        var downstreamIncident = await _db.Incidents
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId
                                   && x.Status    == IncidentStatus.Active);

        if (downstreamIncident == null)
            return;

        // 2. What services does this one depend on?
        var dependencies = await _db.ServiceDependencies
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync();

        if (!dependencies.Any())
            return;

        foreach (var dep in dependencies)
        {
            // 3. Did the upstream service also have an active incident that
            //    started BEFORE ours and within the 5-minute window?
            //    We pick the most recent upstream incident to get the closest cause.
            var upstreamIncident = await _db.Incidents
                .Where(x => x.ServiceId   == dep.DependsOnServiceId
                         && x.Status      == IncidentStatus.Active
                         && x.StartedAtUtc <= downstreamIncident.StartedAtUtc)
                .OrderByDescending(x => x.StartedAtUtc)
                .FirstOrDefaultAsync();

            if (upstreamIncident == null)
                continue;

            var timeDiff = (downstreamIncident.StartedAtUtc - upstreamIncident.StartedAtUtc)
                           .TotalMinutes;

            // Enforce strict time window
            if (timeDiff < 0 || timeDiff > MaxCorrelationWindowMinutes)
                continue;

            // 4. Skip if this exact pair is already recorded
            //    (unique index on the table also enforces this at DB level)
            var alreadyExists = await _db.IncidentCorrelations
                .AnyAsync(x => x.DownstreamIncidentId == downstreamIncident.Id
                            && x.UpstreamIncidentId   == upstreamIncident.Id);

            if (alreadyExists)
                continue;

            // 5. Calculate confidence score
            var confidence = await CalculateConfidenceAsync(
                dep, timeDiff, downstreamIncident, upstreamIncident);

            // 6. Persist the correlation record
            _db.IncidentCorrelations.Add(new IncidentCorrelation
            {
                Id                    = Guid.NewGuid(),
                DownstreamIncidentId  = downstreamIncident.Id,
                UpstreamIncidentId    = upstreamIncident.Id,
                DownstreamServiceId   = serviceId,
                UpstreamServiceId     = dep.DependsOnServiceId,
                ConfidenceScore       = Math.Round(confidence, 2),
                TimeDifferenceMinutes = Math.Round(timeDiff, 2),
                DetectedAtUtc         = DateTime.UtcNow
            });

            // 7. Also update the Incident.Reason so it's human-readable in list views
            //    (only if not already updated — avoids overwriting with worse data)
            if (!downstreamIncident.Reason.Contains("upstream"))
            {
                var upstreamService = await _db.MonitoredServices
                    .FirstOrDefaultAsync(x => x.Id == dep.DependsOnServiceId);

                var name = upstreamService?.Name ?? dep.DependsOnServiceId.ToString();

                downstreamIncident.Reason =
                    $"Likely caused by upstream '{name}' " +
                    $"(confidence: {confidence:P0}, {timeDiff:F1} min gap)";
            }

            await _db.SaveChangesAsync();
        }
    }

    // ── Read — used by IncidentsController ───────────────────────────────────
    public async Task<List<CorrelationResponse>> GetForIncidentAsync(Guid incidentId)
    {
        // Load correlations where this incident is the downstream (it was CAUSED BY something)
        var causedBy = await _db.IncidentCorrelations
            .Include(x => x.UpstreamIncident).ThenInclude(x => x.Service)
            .Include(x => x.DownstreamIncident).ThenInclude(x => x.Service)
            .Where(x => x.DownstreamIncidentId == incidentId)
            .ToListAsync();

        // Load correlations where this incident is the upstream (it CAUSED something else)
        var caused = await _db.IncidentCorrelations
            .Include(x => x.UpstreamIncident).ThenInclude(x => x.Service)
            .Include(x => x.DownstreamIncident).ThenInclude(x => x.Service)
            .Where(x => x.UpstreamIncidentId == incidentId)
            .ToListAsync();

        var result = new List<CorrelationResponse>();

        foreach (var c in causedBy)
            result.Add(MapToResponse(c, direction: "CausedBy"));

        foreach (var c in caused)
            result.Add(MapToResponse(c, direction: "Caused"));

        return result.OrderByDescending(x => x.ConfidenceScore).ToList();
    }

    // ── Confidence scoring ────────────────────────────────────────────────────
    // Three signals, each contributes a weight:
    //
    //   Factor 1 — Explicit dependency (40 pts)
    //     ServiceDependency record exists → full 40 pts
    //
    //   Factor 2 — Time proximity (40 pts)
    //     0 min gap  → 40 pts
    //     5 min gap  →  0 pts  (linear decay)
    //
    //   Factor 3 — Shared failure window (20 pts)
    //     Both services had >=2 failures in the same last-5-checks window → 20 pts
    //
    // Total = 0.0 – 1.0
    private async Task<double> CalculateConfidenceAsync(
        ServiceDependency dep,
        double timeDiffMinutes,
        Incident downstreamIncident,
        Incident upstreamIncident)
    {
        double score = 0.0;

        // Factor 1: explicit dependency always present here (that's how we found the pair)
        score += 0.40;

        // Factor 2: time proximity (linear decay over MaxCorrelationWindowMinutes)
        var timeScore = 0.40 * (1.0 - timeDiffMinutes / MaxCorrelationWindowMinutes);
        score += Math.Max(0.0, timeScore);

        // Factor 3: shared failure pattern — both services had failures in the same window
        var downstreamFailures = await _db.HealthCheckResults
            .Where(x => x.ServiceId == dep.ServiceId && !x.IsSuccess)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(SharedFailureWindow)
            .CountAsync();

        var upstreamFailures = await _db.HealthCheckResults
            .Where(x => x.ServiceId == dep.DependsOnServiceId && !x.IsSuccess)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(SharedFailureWindow)
            .CountAsync();

        if (downstreamFailures >= 2 && upstreamFailures >= 2)
            score += 0.20;

        return Math.Min(score, 1.0);
    }

    // ── Mapper ────────────────────────────────────────────────────────────────
    private static CorrelationResponse MapToResponse(IncidentCorrelation c, string direction) => new()
    {
        Id                    = c.Id,
        DownstreamIncidentId  = c.DownstreamIncidentId,
        DownstreamServiceName = c.DownstreamIncident?.Service?.Name ?? string.Empty,
        UpstreamIncidentId    = c.UpstreamIncidentId,
        UpstreamServiceName   = c.UpstreamIncident?.Service?.Name   ?? string.Empty,
        ConfidenceScore       = c.ConfidenceScore,
        TimeDifferenceMinutes = c.TimeDifferenceMinutes,
        Direction             = direction,
        DetectedAtUtc         = c.DetectedAtUtc
    };
}
