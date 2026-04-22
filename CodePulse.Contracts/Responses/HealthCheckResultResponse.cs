namespace CodePulse.Contracts.Responses;

public class HealthCheckResultResponse
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public int StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAtUtc { get; set; }
}
