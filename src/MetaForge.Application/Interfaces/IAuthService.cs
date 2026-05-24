namespace MetaForge.Application.Interfaces;

/// <summary>
/// User authentication service.
/// </summary>
public interface IAuthService
{
    Task<AuthResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);
}

public class AuthResult
{
    public int UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];
}
