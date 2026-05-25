namespace MetaForge.Application.Authorization;

/// <summary>
/// Current roles and permissions loaded from the database for an authenticated user.
/// </summary>
public sealed class UserAuthorizationSnapshot
{
    public required IReadOnlySet<string> Roles { get; init; }

    public required IReadOnlySet<string> Permissions { get; init; }

    public bool IsAdministrator =>
        Roles.Contains("Administrator", StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string permissionCode) =>
        IsAdministrator || Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}
