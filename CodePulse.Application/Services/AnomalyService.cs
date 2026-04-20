using CodePulse.Contracts.Responses;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Application.Services;

public class AnomalyService : IAnomalyService
{
    private readonly AppDbContext _db;

    // Tuning constants
    private const int    BaselineWindow      = 20;   // checks used to build the baseline
    private const int    MinBaselineSamples  = 10;   // minimum needed before detection fires
    private const double LatencyZScoreThreshold  = 2.5;  // z-score above this → LatencySpike
    private const double ErrorRateDeltaThreshold = 0.15; // error rate delta above this → ErrorRateSpike
    private const double ErrorRateMinCurrent     = 0.10; // current error rate must be at least this
    private const int    ErrorRateWindow         = 10;   // checks per error-rate window
    private const int    FailureStreakWindow      = 3;    // look-back for consecutive failures
    private const int    FailureStreakMinFails    = 2;    // out of FailureStreakWindow → early warning

    public AnomalyService(AppDbContext db)
    {
        _db = db;
    }

    // ── Entry point called by HealthCheckWorker ──────────────────────────────
    public async Task DetectAsync(Guid serviceId)
    {
        await DetectLatencySpikeAsync(serviceId);
        await DetectErrorRateSpikeAsync(serviceId);
        await DetectConsecutiveFailuresAsync(serviceId);
    }

    // ── 1. Latency Spike — Z-Score based ─────────────────────────────────────
    // Formula: z = (current - mean) / stdDev
    // Fires when z > 2.5 (current latency is 2.5 standard deviations above baseline)
    private async Task DetectLatencySpikeAsync(Guid serviceId)
    {
        var checks = await _db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId && x.IsSuccess)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(BaselineWindow)
            .ToListAsync();

        if (checks.Count < MinBaselineSamples)
            return;

        var current  = (double)checks[0].ResponseTimeMs;
        var baseline = checks.Skip(1).Select(x => (double)x.ResponseTimeMs).ToList();

        var mean   = baseline.Average();
        var stdDev = StandardDeviation(baseline);

        // Guard: if std dev is near zero all checks had same latency — use ratio fallback
        double deviation;
        if (stdDev < 1.0)
        {
            if (mean == 0) return;
            deviation = current / mean; // ratio
            if (deviation < 2.0) return;
        }
        else
        {
            deviation = (current - mean) / stdDev; // z-score
            if (deviation < LatencyZScoreThreshold) return;
        }

        // Resolve existing LatencySpike if latency is back to normal
        var existing = await _db.Anomalies
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId
                                   && x.Type   == AnomalyType.LatencySpike
                                   && x.Status == AnomalyStatus.Active);

        if (deviation < LatencyZScoreThreshold && existing != null)
        {
            existing.Status       = AnomalyStatus.Resolved;
            existing.ResolvedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        if (existing != null) return; // already active, don't duplicate

        _db.Anomalies.Add(new Anomaly
        {
            Id            = Guid.NewGuid(),
            ServiceId     = serviceId,
            Type          = AnomalyType.LatencySpike,
            Status        = AnomalyStatus.Active,
            CurrentValue  = current,
            BaselineValue = mean,
            Deviation     = Math.Round(deviation, 2),
            DetectedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ── 2. Error Rate Spike ───────────────────────────────────────────────────
    // Compares error rate of the last N checks (current window)
    // against the previous N checks (historical window).
    // Fires when: current error rate >= 10% AND delta >= 15%
    private async Task DetectErrorRateSpikeAsync(Guid serviceId)
    {
        var checks = await _db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(ErrorRateWindow * 2)
            .ToListAsync();

        if (checks.Count < ErrorRateWindow * 2)
            return;

        var currentWindow    = checks.Take(ErrorRateWindow).ToList();
        var historicalWindow = checks.Skip(ErrorRateWindow).Take(ErrorRateWindow).ToList();

        var currentErrorRate    = currentWindow.Count(x => !x.IsSuccess)    / (double)ErrorRateWindow;
        var historicalErrorRate = historicalWindow.Count(x => !x.IsSuccess) / (double)ErrorRateWindow;
        var delta               = currentErrorRate - historicalErrorRate;

        var existing = await _db.Anomalies
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId
                                   && x.Type   == AnomalyType.ErrorRateSpike
                                   && x.Status == AnomalyStatus.Active);

        // Resolve if error rate returned to near-historical levels
        if (delta < ErrorRateDeltaThreshold && existing != null)
        {
            existing.Status       = AnomalyStatus.Resolved;
            existing.ResolvedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        if (currentErrorRate < ErrorRateMinCurrent || delta < ErrorRateDeltaThreshold)
            return;

        if (existing != null) return;

        _db.Anomalies.Add(new Anomaly
        {
            Id            = Guid.NewGuid(),
            ServiceId     = serviceId,
            Type          = AnomalyType.ErrorRateSpike,
            Status        = AnomalyStatus.Active,
            CurrentValue  = Math.Round(currentErrorRate * 100, 1),   // as %
            BaselineValue = Math.Round(historicalErrorRate * 100, 1), // as %
            Deviation     = Math.Round(delta * 100, 1),               // delta %
            DetectedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ── 3. Consecutive Failure Streak — Early Warning ─────────────────────────
    // Fires when 2 out of the last 3 checks failed.
    // This is an EARLY WARNING — fires before the incident threshold (3 of 3).
    private async Task DetectConsecutiveFailuresAsync(Guid serviceId)
    {
        var checks = await _db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(FailureStreakWindow)
            .ToListAsync();

        if (checks.Count < FailureStreakWindow)
            return;

        var failCount = checks.Count(x => !x.IsSuccess);

        var existing = await _db.Anomalies
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId
                                   && x.Type   == AnomalyType.ConsecutiveFailures
                                   && x.Status == AnomalyStatus.Active);

        // Resolve if streak cleared
        if (failCount < FailureStreakMinFails && existing != null)
        {
            existing.Status       = AnomalyStatus.Resolved;
            existing.ResolvedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        if (failCount < FailureStreakMinFails) return;
        if (existing != null)                  return;

        _db.Anomalies.Add(new Anomaly
        {
            Id            = Guid.NewGuid(),
            ServiceId     = serviceId,
            Type          = AnomalyType.ConsecutiveFailures,
            Status        = AnomalyStatus.Active,
            CurrentValue  = failCount,
            BaselineValue = 0,
            Deviation     = failCount,
            DetectedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ── Read operations (used by AnomaliesController) ─────────────────────────
    public async Task<List<AnomalyResponse>> GetAllAsync()
    {
        var anomalies = await _db.Anomalies
            .Include(x => x.Service)
            .OrderByDescending(x => x.DetectedAtUtc)
            .ToListAsync();

        return anomalies.Select(MapToResponse).ToList();
    }

    public async Task<AnomalyResponse?> GetByIdAsync(Guid id)
    {
        var anomaly = await _db.Anomalies
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id);

        return anomaly is null ? null : MapToResponse(anomaly);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static double StandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;
        var mean      = values.Average();
        var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1)); // sample std dev
    }

    private static AnomalyResponse MapToResponse(Anomaly a) => new()
    {
        Id            = a.Id,
        ServiceId     = a.ServiceId,
        ServiceName   = a.Service?.Name ?? string.Empty,
        Type          = a.Type.ToString(),
        Status        = a.Status.ToString(),
        CurrentValue  = a.CurrentValue,
        BaselineValue = a.BaselineValue,
        Deviation     = a.Deviation,
        DetectedAtUtc = a.DetectedAtUtc,
        ResolvedAtUtc = a.ResolvedAtUtc
    };
}
