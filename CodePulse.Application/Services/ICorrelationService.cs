using CodePulse.Contracts.Responses;

namespace CodePulse.Application.Services;

public interface ICorrelationService
{
    /// <summary>
    /// Called by HealthCheckWorker after each health check.
    /// Checks whether the active incident for this service can be
    /// causally linked to an upstream service's active incident.
    /// </summary>
    Task DetectAsync(Guid serviceId);

    /// <summary>
    /// Returns all correlations where the given incident is
    /// either the upstream cause OR the downstream effect.
    /// </summary>
    Task<List<CorrelationResponse>> GetForIncidentAsync(Guid incidentId);
}
