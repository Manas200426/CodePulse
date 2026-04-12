using CodePulse.Contracts.Requests;
using CodePulse.Contracts.Responses;
using CodePulse.Domain.Entities;
using CodePulse.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodePulse.Application.Services;

public class MonitoredServiceService : IMonitoredServiceService
{
    private readonly AppDbContext _db;

    public MonitoredServiceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MonitoredServiceResponse> CreateAsync(CreateMonitoredServiceRequest request)
    {
        var entity = new MonitoredService
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            HealthEndpoint = request.HealthEndpoint,
            CheckIntervalSeconds = request.CheckIntervalSeconds,
            TimeoutSeconds = request.TimeoutSeconds,
            IsActive = true
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

        if (entity == null) return null;

        return MapToResponse(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateMonitoredServiceRequest request)
    {
        var entity = await _db.MonitoredServices.FindAsync(id);

        if (entity == null) return false;

        entity.Name = request.Name;
        entity.BaseUrl = request.BaseUrl;
        entity.HealthEndpoint = request.HealthEndpoint;
        entity.CheckIntervalSeconds = request.CheckIntervalSeconds;
        entity.TimeoutSeconds = request.TimeoutSeconds;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _db.MonitoredServices.FindAsync(id);

        if (entity == null) return false;

        _db.MonitoredServices.Remove(entity);
        await _db.SaveChangesAsync();

        return true;
    }

    private static MonitoredServiceResponse MapToResponse(MonitoredService entity)
    {
        return new MonitoredServiceResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            HealthEndpoint = entity.HealthEndpoint,
            CheckIntervalSeconds = entity.CheckIntervalSeconds,
            TimeoutSeconds = entity.TimeoutSeconds,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}