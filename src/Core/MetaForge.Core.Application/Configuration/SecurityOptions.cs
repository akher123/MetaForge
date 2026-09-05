namespace MetaForge.Application.Configuration;

/// <summary>
/// Security and password reset settings.
/// </summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Public site URL used in password reset links (e.g. https://app.example.com).
    /// When empty, the current HTTP request origin is used.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>How long reset links remain valid.</summary>
    public int PasswordResetTokenLifetimeHours { get; set; } = 24;

    /// <summary>Minimum password length enforced on reset.</summary>
    public int MinimumPasswordLength { get; set; } = 8;

    /// <summary>Email template code used for invite and forgot-password emails.</summary>
    public string PasswordResetTemplateCode { get; set; } = "password-reset";

    /// <summary>Optional secret appended when hashing reset tokens.</summary>
    public string? TokenPepper { get; set; }
}
