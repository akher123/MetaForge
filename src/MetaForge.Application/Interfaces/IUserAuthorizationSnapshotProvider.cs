using System.Security.Claims;
using MetaForge.Application.Authorization;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Loads the current user's roles and permissions from the database with caching.
/// </summary>
public interface IUserAuthorizationSnapshotProvider
{
    Task<UserAuthorizationSnapshot?> GetSnapshotAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
