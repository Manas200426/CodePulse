namespace CodePulse.Domain.Entities;
using CodePulse.Domain.Enums;
public class Incident
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }

    public MonitoredService Service { get; set; } = null!;

    public IncidentStatus Status { get; set; } = IncidentStatus.Active;

    public string Reason { get; set; } = string.Empty;

    public int FailureCount { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }
}