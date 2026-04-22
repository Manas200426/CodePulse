namespace CodePulse.Contracts.Responses;

public class MetricsResponse
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;

    public int TotalChecks { get; set; }
    public int SuccessfulChecks { get; set; }

    public double UptimePercentage { get; set; }     // e.g. 98.5
    public double AverageResponseMs { get; set; }
    public double P95ResponseMs { get; set; }
    public double P99ResponseMs { get; set; }
    public double ErrorRatePercentage { get; set; }  // e.g. 1.5

    public int ActiveIncidents { get; set; }
    public int ActiveAnomalies { get; set; }

    public long? LatestResponseMs { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}
