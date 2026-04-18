using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MonitoredService = CodePulse.Domain.Entities.MonitoredService;
using DomainHealthCheckResult = CodePulse.Domain.Entities.HealthCheckResult;
using CodePulse.Domain.Entities;


namespace CodePulse.Infrastructure.Monitoring;

public class HealthCheckWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;

    public HealthCheckWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var services = await db.MonitoredServices
                .Where(x => x.IsActive)
                .ToListAsync(stoppingToken);

            foreach (var service in services)
            {
                await CheckServiceAsync(service, db, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task CheckServiceAsync(MonitoredService service, AppDbContext db, CancellationToken token)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var url = $"{service.BaseUrl}{service.HealthEndpoint}";

            var response = await _httpClient.GetAsync(url, token);

            stopwatch.Stop();

            var result = new DomainHealthCheckResult
            {
                Id = Guid.NewGuid(),
                ServiceId = service.Id,
                StatusCode = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = response.IsSuccessStatusCode,
                CheckedAtUtc = DateTime.UtcNow
            };

            db.HealthCheckResults.Add(result);
            await db.SaveChangesAsync(token);
            await DetectIncidentAsync(service.Id, db);
            await DetectLatencyAnomalyAsync(service.Id, db);
            await DetectCorrelationAsync(service.Id, db);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var result = new DomainHealthCheckResult
            {
                Id = Guid.NewGuid(),
                ServiceId = service.Id,
                StatusCode = 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                CheckedAtUtc = DateTime.UtcNow
            };

            db.HealthCheckResults.Add(result);
            await db.SaveChangesAsync(token);
        }
    }
    private async Task DetectIncidentAsync(Guid serviceId, AppDbContext db)
    {
        var lastChecks = await db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(3)
            .ToListAsync();

        if (lastChecks.Count < 3)
            return;

        var existingIncident = await db.Incidents
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.Status == "Active");

        var allFailed = lastChecks.All(x => !x.IsSuccess);
        var allSuccess = lastChecks.All(x => x.IsSuccess);

        // 🔴 CREATE INCIDENT
        if (allFailed && existingIncident == null)
        {
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                ServiceId = serviceId,
                Status = "Active",
                Reason = "Service failed 3 consecutive health checks",
                FailureCount = 3,
                StartedAtUtc = DateTime.UtcNow
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync();
        }

        // 🟢 RESOLVE INCIDENT
        if (allSuccess && existingIncident != null)
        {
            existingIncident.Status = "Resolved";
            existingIncident.ResolvedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }
    }

    private async Task DetectLatencyAnomalyAsync(Guid serviceId, AppDbContext db)
    {
        var recentChecks = await db.HealthCheckResults
            .Where(x => x.ServiceId == serviceId && x.IsSuccess)
            .OrderByDescending(x => x.CheckedAtUtc)
            .Take(20)
            .ToListAsync();

        if (recentChecks.Count < 10)
            return;

        var current = recentChecks.First().ResponseTimeMs;

        var baseline = recentChecks.Skip(1).Average(x => x.ResponseTimeMs);

        if (baseline == 0)
            return;

        var deviation = current / baseline;

        if (deviation < 2.0)
            return;

        var existing = await db.Anomalies
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.Status == "Active");

        if (existing != null)
            return;

        var anomaly = new Anomaly
        {
            Id = Guid.NewGuid(),
            ServiceId = serviceId,
            Type = "LatencySpike",
            CurrentValue = current,
            BaselineValue = baseline,
            Deviation = deviation,
            DetectedAtUtc = DateTime.UtcNow
        };

        db.Anomalies.Add(anomaly);
        await db.SaveChangesAsync();
    }
    private async Task DetectCorrelationAsync(Guid serviceId, AppDbContext db)
    {
        var currentIncident = await db.Incidents
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.Status == "Active");

        if (currentIncident == null)
            return;

        var dependencies = await db.ServiceDependencies
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync();

        foreach (var dep in dependencies)
        {
            var upstreamIncident = await db.Incidents
                .Where(x => x.ServiceId == dep.DependsOnServiceId && x.Status == "Active")
                .OrderBy(x => x.StartedAtUtc)
                .FirstOrDefaultAsync();

            if (upstreamIncident == null)
                continue;

            // check time difference
            if (upstreamIncident.StartedAtUtc < currentIncident.StartedAtUtc)
            {
                currentIncident.Reason = $"Likely caused by upstream service {dep.DependsOnServiceId}";
                await db.SaveChangesAsync();
            }
        }
    }
}