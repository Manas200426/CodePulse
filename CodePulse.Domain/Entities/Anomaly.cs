namespace CodePulse.Domain.Entities;

public class Anomaly
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }

    public MonitoredService Service { get; set; } = null!;

    public string Type { get; set; } = string.Empty; // LatencySpike

    public double CurrentValue { get; set; }

    public double BaselineValue { get; set; }

    public double Deviation { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}