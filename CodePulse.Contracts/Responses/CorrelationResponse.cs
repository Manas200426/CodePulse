namespace CodePulse.Contracts.Responses;

public class CorrelationResponse
{
    public Guid Id { get; set; }

    public Guid DownstreamIncidentId { get; set; }
    public string DownstreamServiceName { get; set; } = string.Empty;

    public Guid UpstreamIncidentId { get; set; }
    public string UpstreamServiceName { get; set; } = string.Empty;

    /// <summary>0.0 – 1.0. Higher = more confident this is a causal link.</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>How many minutes after the upstream incident did this one start.</summary>
    public double TimeDifferenceMinutes { get; set; }

    /// <summary>"CausedBy" or "Caused" — relative to the incident being queried.</summary>
    public string Direction { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }
}
