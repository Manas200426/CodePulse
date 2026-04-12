namespace CodePulse.Contracts.Requests;

public class CreateMonitoredServiceRequest
{
    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string HealthEndpoint { get; set; } = string.Empty;

    public int CheckIntervalSeconds { get; set; }

    public int TimeoutSeconds { get; set; }
}