namespace MetaForge.Application.Interfaces;

/// <summary>
/// Rotates user security stamps to invalidate cached authorization state.
/// </summary>
public interface ISecurityStampService
{
    string GenerateStamp();

    Task EnsureUserHasStampAsync(int userId, CancellationToken cancellationToken = default);

    Task BumpUserStampAsync(int userId, CancellationToken cancellationToken = default);

    Task BumpUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default);
}
