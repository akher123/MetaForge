namespace MetaForge.Application.Interfaces;

/// <summary>
/// Issues and validates password reset tokens and sends reset emails.
/// </summary>
public interface IPasswordResetService
{
    Task SendNewUserInviteAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a reset email when a matching active user exists.
    /// Always completes without revealing whether the account exists.
    /// </summary>
    Task SendForgotPasswordAsync(string emailOrUserName, CancellationToken cancellationToken = default);

    Task<PasswordResetTokenInfo?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}

public class PasswordResetTokenInfo
{
    public int UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;
}
