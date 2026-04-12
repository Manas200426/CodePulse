namespace CodePulse.Domain.Entities;

public class MonitoredService
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string HealthEndpoint { get; set; } = string.Empty;

    public int CheckIntervalSeconds { get; set; }

    public int TimeoutSeconds { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}