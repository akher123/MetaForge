namespace MetaForge.Domain.Notifications;

/// <summary>
/// Configurable email transport channel (SMTP, SendGrid, etc.).
/// </summary>
public class EmailChannel
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Provider type: <see cref="Enums.EmailProviderType"/>.</summary>
    public string Provider { get; set; } = Enums.EmailProviderType.Smtp;

    public string FromAddress { get; set; } = string.Empty;

    public string? FromDisplayName { get; set; }

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public bool SmtpUseSsl { get; set; } = true;

    public string? SmtpUsername { get; set; }

    /// <summary>
    /// Key into <c>Email:Secrets</c> in appsettings for SMTP password or SendGrid API key.
    /// </summary>
    public string? CredentialSecretName { get; set; }

    public int MaxDegreeOfParallelism { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public ICollection<EmailTemplate> Templates { get; set; } = [];
}
