namespace MetaForge.Domain.Notifications;

/// <summary>
/// Outbound email outbox row with delivery status and retry tracking.
/// </summary>
public class EmailMessage
{
    public int Id { get; set; }

    public int? EmailTemplateId { get; set; }

    public int EmailChannelId { get; set; }

    public int RetryPolicyId { get; set; }

    public string ToAddress { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    public string? BodyText { get; set; }

    public string Status { get; set; } = Enums.EmailStatus.Queued;

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? SentUtc { get; set; }

    public string? LastError { get; set; }

    public string? SourceEntity { get; set; }

    public string? SourceRecordId { get; set; }

    public string? ContextJson { get; set; }

    public EmailTemplate? EmailTemplate { get; set; }

    public EmailChannel EmailChannel { get; set; } = null!;

    public EmailRetryPolicy RetryPolicy { get; set; } = null!;
}
