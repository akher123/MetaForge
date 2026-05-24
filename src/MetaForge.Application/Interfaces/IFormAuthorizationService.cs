using System.Security.Claims;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Form-scoped authorization service.
/// </summary>
public interface IFormAuthorizationService
{
    Task<bool> HasPermissionAsync(int userId, string formCode, string action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> HasFormPermissionAsync(ClaimsPrincipal user, string formCode, string action, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionCodeAsync(ClaimsPrincipal user, string permissionCode, CancellationToken cancellationToken = default);

    Task<FormPermissionsDto> GetFormPermissionsAsync(ClaimsPrincipal user, string formCode, CancellationToken cancellationToken = default);

    Task<string?> ResolveFormCodeByEntityAsync(string entityName, CancellationToken cancellationToken = default);
}
