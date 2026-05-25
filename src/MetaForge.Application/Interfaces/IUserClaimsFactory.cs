using System.Security.Claims;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Builds cookie authentication principals for sign-in and session refresh.
/// </summary>
public interface IUserClaimsFactory
{
    Task<ClaimsPrincipal?> CreatePrincipalAsync(int userId, CancellationToken cancellationToken = default);
}
