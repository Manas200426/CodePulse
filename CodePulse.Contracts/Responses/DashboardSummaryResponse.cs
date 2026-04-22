namespace CodePulse.Contracts.Responses;

public class DashboardSummaryResponse
{
    public int TotalServices { get; set; }
    public int ActiveIncidents { get; set; }
    public int ActiveAnomalies { get; set; }

    /// <summary>No active incident and no active anomaly.</summary>
    public int HealthyServices { get; set; }

    /// <summary>Active anomaly but no active incident (degraded but still responding).</summary>
    public int DegradedServices { get; set; }

    /// <summary>Has an active incident (3 consecutive failures).</summary>
    public int DownServices { get; set; }

    public List<ServiceStatusSummary> Services { get; set; } = [];
}

public class ServiceStatusSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>"Healthy" | "Degraded" | "Down" | "Unknown"</summary>
    public string HealthStatus { get; set; } = string.Empty;

    public bool HasActiveIncident { get; set; }
    public bool HasActiveAnomaly { get; set; }
    public long? LatestResponseMs { get; set; }
    public bool? LastCheckSuccess { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
}
