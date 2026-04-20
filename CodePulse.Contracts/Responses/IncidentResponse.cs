namespace CodePulse.Contracts.Responses;

public class IncidentResponse
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int FailureCount { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
