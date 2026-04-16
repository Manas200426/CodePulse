namespace CodePulse.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }

    public MonitoredService Service { get; set; } = null!;

    public string Status { get; set; } = "Active"; // Active / Resolved

    public string Reason { get; set; } = string.Empty;

    public int FailureCount { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }
}