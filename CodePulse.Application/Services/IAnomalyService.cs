using CodePulse.Contracts.Responses;

namespace CodePulse.Application.Services;

public interface IAnomalyService
{
    /// <summary>
    /// Runs all 3 detectors for a service after a health check result is saved.
    /// Called by HealthCheckWorker.
    /// </summary>
    Task DetectAsync(Guid serviceId);

    Task<List<AnomalyResponse>> GetAllAsync();

    Task<AnomalyResponse?> GetByIdAsync(Guid id);
}
