namespace CodePulse.Domain.Entities;

public class ServiceDependency
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }        // dependent service
    public MonitoredService Service { get; set; } = null!;

    public Guid DependsOnServiceId { get; set; } // upstream service
}