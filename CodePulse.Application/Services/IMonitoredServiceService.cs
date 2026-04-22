using CodePulse.Contracts.Requests;
using CodePulse.Contracts.Responses;

namespace CodePulse.Application.Services;

public interface IMonitoredServiceService
{
    Task<MonitoredServiceResponse> CreateAsync(CreateMonitoredServiceRequest request);

    Task<List<MonitoredServiceResponse>> GetAllAsync();

    Task<MonitoredServiceResponse?> GetByIdAsync(Guid id);

    Task<bool> UpdateAsync(Guid id, UpdateMonitoredServiceRequest request);

    Task<bool> DeleteAsync(Guid id);

    /// <summary>Trigger an immediate health check for the given service, save the result, run all detectors.</summary>
    Task<HealthCheckResultResponse?> RunCheckAsync(Guid id);

    /// <summary>Returns aggregated metrics (uptime %, latency percentiles, error rate, etc.).</summary>
    Task<MetricsResponse?> GetMetricsAsync(Guid id);
}