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
}