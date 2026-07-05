namespace MetaForge.Domain.Security;

/// <summary>
/// Single-use, time-limited token for setting or resetting a user password.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>SHA-256 hash of the raw token (never store the token itself).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Why the token was issued (e.g. NewUserInvite, ForgotPassword).</summary>
    public string Purpose { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public DateTime? UsedUtc { get; set; }
}
