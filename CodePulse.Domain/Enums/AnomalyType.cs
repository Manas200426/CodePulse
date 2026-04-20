namespace CodePulse.Domain.Enums;

public enum AnomalyType
{
    LatencySpike           = 1,  // response time >> rolling baseline (z-score)
    ErrorRateSpike         = 2,  // error rate jumped in recent window vs historical
    ConsecutiveFailures    = 3   // 2 of last 3 checks failed — early warning before incident
}
