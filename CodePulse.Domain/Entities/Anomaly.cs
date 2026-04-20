using CodePulse.Domain.Enums;

namespace CodePulse.Domain.Entities;

public class Anomaly
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }
    public MonitoredService Service { get; set; } = null!;

    public AnomalyType Type { get; set; }

    public double CurrentValue { get; set; }   // e.g. current latency ms / current error rate %

    public double BaselineValue { get; set; }  // rolling mean

    public double Deviation { get; set; }      // z-score (latency) or delta % (error rate)

    public AnomalyStatus Status { get; set; } = AnomalyStatus.Active;

    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }
}
