using CodePulse.Contracts.Requests;
using CodePulse.Contracts.Responses;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Application.Services;

public class MonitoredServiceService : IMonitoredServiceService
{
    private readonly AppDbContext          _db;
    private readonly IHttpClientFactory    _httpClientFactory;
    private readonly IIncidentService      _incidentService;
    private readonly IAnomalyService       _anomalyService;
    private readonly ICorrelationService   _correlationService;

    public MonitoredServiceService(
        AppDbContext        db,
        IHttpClientFactory  httpClientFactory,
        IIncidentService    incidentService,
        IAnomalyService     anomalyService,
        ICorrelationService correlationService)
    {
        _db                 = db;
        _httpClientFactory  = httpClientFactory;
        _incidentService    = incidentService;
        _anomalyService     = anomalyService;
        _correlationService = correlationService;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────
    public async Task<MonitoredServiceResponse> CreateAsync(CreateMonitoredServiceRequest request)
    {
        var entity = new MonitoredService
        {
            Id                   = Guid.NewGuid(),
            Name                 = request.Name,
            BaseUrl              = request.BaseUrl,
            HealthEndpoint       = request.HealthEndpoint,
            CheckIntervalSeconds = request.CheckIntervalSeconds,
            TimeoutSeconds       = request.TimeoutSeconds,
            IsActive             = true
        };

        _db.MonitoredServices.Add(entity);
        await _db.SaveChangesAsync();

        return MapToResponse(entity);
    }

    public async Task<List<MonitoredServiceResponse>> GetAllAsync()
    {
        var services = await _db.MonitoredServices.ToListAsync();
        return services.Select(MapToResponse).ToList();
    }

    public async Task<MonitoredServiceResponse?> GetByIdAsync(Guid id)
    {
        var entity = await _db.MonitoredServices.FindAsync(id);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateMonitoredServiceRequest request)
    {
        var entity = await _db.MonitoredServices.FindAsync(id);
        if (entity is null) return false;

        entity.Name                 = request.Name;
        entity.BaseUrl              = request.BaseUrl;
        entity.HealthEndpoint       = request.HealthEndpoint;
        entity.CheckIntervalSeconds = request.CheckIntervalSeconds;
        entity.TimeoutSeconds       = request.TimeoutSeconds;
        entity.IsActive             = request.IsActive;
        entity.UpdatedAtUtc         = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _db.MonitoredServices.FindAsync(id);
        if (entity is null) return false;

        _db.MonitoredServices.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Manual health check trigger ───────────────────────────────────────────
    public async Task<HealthCheckResultResponse?> RunCheckAsync(Guid id)
    {
        var service = await _db.MonitoredServices.FindAsync(id);
        if (service is null) return null;

        var url       = $"{service.BaseUrl}{service.HealthEndpoint}";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        HealthCheckResult result;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(service.TimeoutSeconds));
            var response = await client.GetAsync(url, cts.Token);
            stopwatch.Stop();

            result = new HealthCheckResult
            {
                Id             = Guid.NewGuid(),
                ServiceId      = service.Id,
                StatusCode     = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess      = response.IsSuccessStatusCode,
                CheckedAtUtc   = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            result = new HealthCheckResult
            {
                Id             = Guid.NewGuid(),
                ServiceId      = service.Id,
                StatusCode     = 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess      = false,
                ErrorMessage   = ex.Message,
                CheckedAtUtc   = DateTime.UtcNow
            };
        }

        _db.HealthCheckResults.Add(result);
        await _db.SaveChangesAsync();

        // Run all detectors exactly like the background worker does
        await _incidentService.DetectAsync(service.Id);
        await _anomalyService.DetectAsync(service.Id);
        await _correlationService.DetectAsync(service.Id);

        return new HealthCheckResultResponse
        {
            Id             = result.Id,
            ServiceId      = result.ServiceId,
            StatusCode     = result.StatusCode,
            ResponseTimeMs = result.ResponseTimeMs,
            IsSuccess      = result.IsSuccess,
            ErrorMessage   = result.ErrorMessage,
            CheckedAtUtc   = result.CheckedAtUtc
        };
    }

    // ── Aggregated metrics ────────────────────────────────────────────────────
    public async Task<MetricsResponse?> GetMetricsAsync(Guid id)
    {
        var service = await _db.MonitoredServices.FindAsync(id);
        if (service is null) return null;

        var checks = await _db.HealthCheckResults
            .Where(x => x.ServiceId == id)
            .OrderByDescending(x => x.CheckedAtUtc)
            .ToListAsync();

        var activeIncidents = await _db.Incidents
            .CountAsync(x => x.ServiceId == id && x.Status == IncidentStatus.Active);

        var activeAnomalies = await _db.Anomalies
            .CountAsync(x => x.ServiceId == id && x.Status == AnomalyStatus.Active);

        if (!checks.Any())
        {
            return new MetricsResponse
            {
                ServiceId       = service.Id,
                ServiceName     = service.Name,
                ActiveIncidents = activeIncidents,
                ActiveAnomalies = activeAnomalies
            };
        }

        var total      = checks.Count;
        var successful = checks.Count(x => x.IsSuccess);
        var sortedMs   = checks.Select(x => (double)x.ResponseTimeMs).OrderBy(x => x).ToList();

        return new MetricsResponse
        {
            ServiceId            = service.Id,
            ServiceName          = service.Name,
            TotalChecks          = total,
            SuccessfulChecks     = successful,
            UptimePercentage     = Math.Round((double)successful / total * 100, 2),
            AverageResponseMs    = Math.Round(sortedMs.Average(), 2),
            P95ResponseMs        = Percentile(sortedMs, 95),
            P99ResponseMs        = Percentile(sortedMs, 99),
            ErrorRatePercentage  = Math.Round((double)(total - successful) / total * 100, 2),
            ActiveIncidents      = activeIncidents,
            ActiveAnomalies      = activeAnomalies,
            LatestResponseMs     = checks[0].ResponseTimeMs,
            LastCheckedAtUtc     = checks[0].CheckedAtUtc
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    /// <summary>Returns the pth percentile from a pre-sorted ascending list.</summary>
    private static double Percentile(List<double> sorted, int p)
    {
        if (!sorted.Any()) return 0;
        var index = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
        return Math.Round(sorted[Math.Clamp(index, 0, sorted.Count - 1)], 2);
    }

    private static MonitoredServiceResponse MapToResponse(MonitoredService entity) => new()
    {
        Id                   = entity.Id,
        Name                 = entity.Name,
        BaseUrl              = entity.BaseUrl,
        HealthEndpoint       = entity.HealthEndpoint,
        CheckIntervalSeconds = entity.CheckIntervalSeconds,
        TimeoutSeconds       = entity.TimeoutSeconds,
        IsActive             = entity.IsActive,
        CreatedAtUtc         = entity.CreatedAtUtc
    };
}
