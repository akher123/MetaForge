namespace MetaForge.Domain.Notifications;

using MetaForge.Domain.Metadata;

/// <summary>
/// Links an email template to a feature form and trigger event.
/// </summary>
public class EmailTemplateBinding
{
    public int Id { get; set; }

    public int EmailTemplateId { get; set; }

    public int FormId { get; set; }

    public string TriggerEvent { get; set; } = Enums.EmailTriggerEvent.OnAction;

    /// <summary>When trigger is OnAction, matches <see cref="Metadata.ForgeFormAction.Code"/>.</summary>
    public string? ActionCode { get; set; }

    /// <summary>Entity property holding the recipient address, e.g. Email.</summary>
    public string? RecipientField { get; set; }

    public string? ConditionExpression { get; set; }

    public bool IsActive { get; set; } = true;

    public EmailTemplate EmailTemplate { get; set; } = null!;

    public ForgeForm Form { get; set; } = null!;
}
