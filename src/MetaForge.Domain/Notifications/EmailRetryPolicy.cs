namespace MetaForge.Domain.Notifications;

/// <summary>
/// Retry policy for failed email delivery attempts.
/// </summary>
public class EmailRetryPolicy
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int MaxAttempts { get; set; } = 5;

    public string BackoffStrategy { get; set; } = Enums.EmailBackoffStrategy.Exponential;

    public int BaseDelaySeconds { get; set; } = 60;

    public int MaxDelaySeconds { get; set; } = 3600;

    public double BackoffMultiplier { get; set; } = 2.0;

    public bool UseJitter { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }
}
