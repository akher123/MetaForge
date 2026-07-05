namespace MetaForge.Infrastructure.Email;

public sealed class RetryPolicyEvaluator : IRetryPolicyEvaluator
{
    public TimeSpan? GetNextDelay(EmailRetryPolicy policy, int attemptCount)
    {
        if (attemptCount >= policy.MaxAttempts)
            return null;

        double seconds = policy.BackoffStrategy switch
        {
            EmailBackoffStrategy.Fixed => policy.BaseDelaySeconds,
            EmailBackoffStrategy.Linear => policy.BaseDelaySeconds * attemptCount,
            EmailBackoffStrategy.Exponential => policy.BaseDelaySeconds * Math.Pow(policy.BackoffMultiplier, attemptCount - 1),
            _ => policy.BaseDelaySeconds
        };

        seconds = Math.Min(seconds, policy.MaxDelaySeconds);

        if (policy.UseJitter)
            seconds *= 0.8 + Random.Shared.NextDouble() * 0.4;

        return TimeSpan.FromSeconds(seconds);
    }
}
