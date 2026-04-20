using CodePulse.Application.Services;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MonitoredService = CodePulse.Domain.Entities.MonitoredService;
using DomainHealthCheckResult = CodePulse.Domain.Entities.HealthCheckResult;


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
            var db              = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var incidentService = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            var anomalyService  = scope.ServiceProvider.GetRequiredService<IAnomalyService>();

            var services = await db.MonitoredServices
                .Where(x => x.IsActive)
                .ToListAsync(stoppingToken);

            foreach (var service in services)
            {
                await CheckServiceAsync(service, db, incidentService, anomalyService, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task CheckServiceAsync(MonitoredService service, AppDbContext db, IIncidentService incidentService, IAnomalyService anomalyService, CancellationToken token)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var url      = $"{service.BaseUrl}{service.HealthEndpoint}";
            var response = await _httpClient.GetAsync(url, token);
            stopwatch.Stop();

            var result = new DomainHealthCheckResult
            {
                Id             = Guid.NewGuid(),
                ServiceId      = service.Id,
                StatusCode     = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess      = response.IsSuccessStatusCode,
                CheckedAtUtc   = DateTime.UtcNow
            };

            db.HealthCheckResults.Add(result);
            await db.SaveChangesAsync(token);

            await incidentService.DetectAsync(service.Id);
            await anomalyService.DetectAsync(service.Id);
            await DetectCorrelationAsync(service.Id, db);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var result = new DomainHealthCheckResult
            {
                Id             = Guid.NewGuid(),
                ServiceId      = service.Id,
                StatusCode     = 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess      = false,
                ErrorMessage   = ex.Message,
                CheckedAtUtc   = DateTime.UtcNow
            };

            db.HealthCheckResults.Add(result);
            await db.SaveChangesAsync(token);

            await incidentService.DetectAsync(service.Id);
            await anomalyService.DetectAsync(service.Id);
        }
    }
    private async Task DetectCorrelationAsync(Guid serviceId, AppDbContext db)
    {
        var currentIncident = await db.Incidents
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.Status == IncidentStatus.Active);

        if (currentIncident == null)
            return;

        var dependencies = await db.ServiceDependencies
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync();

        if (!dependencies.Any())
            return;

        foreach (var dep in dependencies)
        {
            var upstreamIncident = await db.Incidents
                .Where(x => x.ServiceId == dep.DependsOnServiceId && x.Status == IncidentStatus.Active)
                .OrderBy(x => x.StartedAtUtc)
                .FirstOrDefaultAsync();

            if (upstreamIncident == null)
                continue;

            // 🧠 TIME WINDOW CHECK (important)
            var timeDiff = (currentIncident.StartedAtUtc - upstreamIncident.StartedAtUtc).TotalMinutes;

            if (timeDiff < 0 || timeDiff > 5)
                continue;

            // 🧠 UPDATE ONLY IF NOT ALREADY CORRELATED
            if (!currentIncident.Reason.Contains("upstream"))
            {
                currentIncident.Reason =
                    $"Likely caused by upstream service {dep.DependsOnServiceId} (within {timeDiff:F1} min window)";

                await db.SaveChangesAsync();
            }
        }
    }
}