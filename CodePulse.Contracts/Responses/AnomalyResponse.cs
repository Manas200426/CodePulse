namespace CodePulse.Contracts.Responses;

public class AnomalyResponse
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double BaselineValue { get; set; }
    public double Deviation { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
