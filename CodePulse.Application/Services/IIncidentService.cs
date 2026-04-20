using CodePulse.Contracts.Responses;

namespace CodePulse.Application.Services;

public interface IIncidentService
{
    /// <summary>Called by HealthCheckWorker after every health check result is saved.</summary>
    Task DetectAsync(Guid serviceId);

    Task<List<IncidentResponse>> GetAllAsync();

    Task<IncidentResponse?> GetByIdAsync(Guid id);
}
