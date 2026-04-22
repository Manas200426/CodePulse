using CodePulse.Application.Services;
using CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

using MonitoredService = CodePulse.Domain.Entities.MonitoredService;
using DomainHealthCheckResult = CodePulse.Domain.Entities.HealthCheckResult;

namespace CodePulse.Infrastructure.Monitoring;

public class HealthCheckWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;

    // Tracks when each service was last checked so we can respect CheckIntervalSeconds
    private readonly ConcurrentDictionary<Guid, DateTime> _lastCheckTimes = new();

    // How often the worker "ticks" to see which services are due — 10s is fine
    private const int TickIntervalSeconds = 10;

    public HealthCheckWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _httpClient      = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var db              = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var incidentService    = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            var anomalyService     = scope.ServiceProvider.GetRequiredService<IAnomalyService>();
            var correlationService = scope.ServiceProvider.GetRequiredService<ICorrelationService>();

            var services = await db.MonitoredServices
                .Where(x => x.IsActive)
                .ToListAsync(stoppingToken);

            var now = DateTime.UtcNow;

            foreach (var service in services)
            {
                // Only check if enough time has passed since the last check for this service
                _lastCheckTimes.TryGetValue(service.Id, out var lastCheck);
                var secondsSinceLastCheck = (now - lastCheck).TotalSeconds;

                if (secondsSinceLastCheck < service.CheckIntervalSeconds)
                    continue;

                _lastCheckTimes[service.Id] = now;

                await CheckServiceAsync(service, db, incidentService, anomalyService, correlationService, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(TickIntervalSeconds), stoppingToken);
        }
    }

    private async Task CheckServiceAsync(
        MonitoredService service,
        AppDbContext db,
        IIncidentService incidentService,
        IAnomalyService anomalyService,
        ICorrelationService correlationService,
        CancellationToken token)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var url = $"{service.BaseUrl}{service.HealthEndpoint}";

            // Respect the per-service timeout — cancel if it takes too long
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(service.TimeoutSeconds));

            var response = await _httpClient.GetAsync(url, timeoutCts.Token);
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
            await correlationService.DetectAsync(service.Id);
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
            await correlationService.DetectAsync(service.Id);
        }
    }
}
