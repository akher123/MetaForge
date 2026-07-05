namespace MetaForge.Application.Configuration;

/// <summary>
/// Global email settings and secret resolution.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public bool SendingEnabled { get; set; } = true;

    public int RetrySweepIntervalSeconds { get; set; } = 30;

    /// <summary>Maps secret name to SMTP password or SendGrid API key.</summary>
    public Dictionary<string, string> Secrets { get; set; } = new();
}
