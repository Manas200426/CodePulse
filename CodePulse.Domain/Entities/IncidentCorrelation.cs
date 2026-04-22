namespace CodePulse.Domain.Entities;

public class IncidentCorrelation
{
    public Guid Id { get; set; }

    // The effect — the incident we are explaining
    public Guid DownstreamIncidentId { get; set; }
    public Incident DownstreamIncident { get; set; } = null!;

    // The probable cause
    public Guid UpstreamIncidentId { get; set; }
    public Incident UpstreamIncident { get; set; } = null!;

    // Denormalised so queries don't need joins on incident just to filter by service
    public Guid DownstreamServiceId { get; set; }
    public Guid UpstreamServiceId { get; set; }

    /// <summary>
    /// 0.0 – 1.0.  Derived from: dependency mapping + time proximity + shared failure window.
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Minutes between upstream incident start and downstream incident start.
    /// Always >= 0 (upstream must have started first).
    /// </summary>
    public double TimeDifferenceMinutes { get; set; }

    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
