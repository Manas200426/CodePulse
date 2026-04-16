namespace CodePulse.Domain.Entities;

public class HealthCheckResult
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }

    public MonitoredService Service { get; set; } = null!;

    public int StatusCode { get; set; }

    public long ResponseTimeMs { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}