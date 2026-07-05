namespace MetaForge.Domain.Enums;

/// <summary>
/// Supported email transport providers configured per channel.
/// </summary>
public static class EmailProviderType
{
    public const string Smtp = "Smtp";
    public const string SendGrid = "SendGrid";

    public static readonly IReadOnlyList<string> All = [Smtp, SendGrid];
}

/// <summary>
/// Lifecycle status of an outbound email message.
/// </summary>
public static class EmailStatus
{
    public const string Queued = "Queued";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Retrying = "Retrying";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [Queued, Sending, Sent, Retrying, Failed, Cancelled];
}

/// <summary>
/// Retry backoff strategies configured per retry policy.
/// </summary>
public static class EmailBackoffStrategy
{
    public const string Fixed = "Fixed";
    public const string Linear = "Linear";
    public const string Exponential = "Exponential";

    public static readonly IReadOnlyList<string> All = [Fixed, Linear, Exponential];
}

/// <summary>
/// Events that can trigger an email from a feature form binding.
/// </summary>
public static class EmailTriggerEvent
{
    public const string OnCreate = "OnCreate";
    public const string OnUpdate = "OnUpdate";
    public const string OnDelete = "OnDelete";
    public const string OnAction = "OnAction";

    public static readonly IReadOnlyList<string> All = [OnCreate, OnUpdate, OnDelete, OnAction];
}
