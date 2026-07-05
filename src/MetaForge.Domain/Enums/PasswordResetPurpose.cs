namespace MetaForge.Domain.Enums;

/// <summary>
/// Reasons a password reset token may be issued.
/// </summary>
public static class PasswordResetPurpose
{
    public const string NewUserInvite = "NewUserInvite";
    public const string ForgotPassword = "ForgotPassword";

    public static readonly IReadOnlyList<string> All = [NewUserInvite, ForgotPassword];
}
