namespace MetaForge.Domain.Notifications;

/// <summary>
/// HTML email template with placeholder tokens (e.g. {{CustomerName}}).
/// </summary>
public class EmailTemplate
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    public string? BodyText { get; set; }

    /// <summary>Token expression for default recipient, e.g. {{Email}}.</summary>
    public string? DefaultToExpression { get; set; }

    public string? DefaultCc { get; set; }

    public string? DefaultBcc { get; set; }

    public int? EmailChannelId { get; set; }

    public int? RetryPolicyId { get; set; }

    public string Culture { get; set; } = "en";

    public bool IsActive { get; set; } = true;

    public EmailChannel? EmailChannel { get; set; }

    public EmailRetryPolicy? RetryPolicy { get; set; }

    public ICollection<EmailTemplateBinding> Bindings { get; set; } = [];
}
