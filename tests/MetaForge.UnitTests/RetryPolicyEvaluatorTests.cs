using MetaForge.Domain.Notifications;
using MetaForge.Infrastructure.Email;

namespace MetaForge.UnitTests;

public class RetryPolicyEvaluatorTests
{
    private readonly RetryPolicyEvaluator _evaluator = new();

    [Fact]
    public void GetNextDelay_ReturnsNull_WhenAttemptsExhausted()
    {
        var policy = new EmailRetryPolicy { MaxAttempts = 3, BaseDelaySeconds = 60, BackoffStrategy = EmailBackoffStrategy.Fixed };

        var delay = _evaluator.GetNextDelay(policy, 3);

        Assert.Null(delay);
    }

    [Fact]
    public void GetNextDelay_FixedStrategy_ReturnsBaseDelay()
    {
        var policy = new EmailRetryPolicy
        {
            MaxAttempts = 5,
            BaseDelaySeconds = 120,
            BackoffStrategy = EmailBackoffStrategy.Fixed,
            UseJitter = false
        };

        var delay = _evaluator.GetNextDelay(policy, 2);

        Assert.Equal(TimeSpan.FromSeconds(120), delay);
    }

    [Fact]
    public void GetNextDelay_LinearStrategy_ScalesWithAttemptCount()
    {
        var policy = new EmailRetryPolicy
        {
            MaxAttempts = 5,
            BaseDelaySeconds = 30,
            BackoffStrategy = EmailBackoffStrategy.Linear,
            UseJitter = false
        };

        var delay = _evaluator.GetNextDelay(policy, 3);

        Assert.Equal(TimeSpan.FromSeconds(90), delay);
    }

    [Fact]
    public void GetNextDelay_ExponentialStrategy_AppliesMultiplier()
    {
        var policy = new EmailRetryPolicy
        {
            MaxAttempts = 5,
            BaseDelaySeconds = 10,
            BackoffStrategy = EmailBackoffStrategy.Exponential,
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        var delay = _evaluator.GetNextDelay(policy, 3);

        Assert.Equal(TimeSpan.FromSeconds(40), delay);
    }

    [Fact]
    public void GetNextDelay_CapsAtMaxDelay()
    {
        var policy = new EmailRetryPolicy
        {
            MaxAttempts = 10,
            BaseDelaySeconds = 1000,
            MaxDelaySeconds = 300,
            BackoffStrategy = EmailBackoffStrategy.Fixed,
            UseJitter = false
        };

        var delay = _evaluator.GetNextDelay(policy, 1);

        Assert.Equal(TimeSpan.FromSeconds(300), delay);
    }
}
