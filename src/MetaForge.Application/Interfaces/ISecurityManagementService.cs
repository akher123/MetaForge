namespace MetaForge.Application.Interfaces;

/// <summary>
/// User, role, and permission management service.
/// </summary>
public interface ISecurityManagementService
{
    Task<SecurityOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserManagementDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserManagementDto?> GetUserAsync(int id, CancellationToken cancellationToken = default);

    Task<SaveUserDto?> GetUserForEditAsync(int id, CancellationToken cancellationToken = default);

    Task<int> SaveUserAsync(SaveUserDto user, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleManagementDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<RoleDetailDto?> GetRoleAsync(int id, CancellationToken cancellationToken = default);

    Task<int> SaveRoleAsync(SaveRoleDto role, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGroupDto>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default);

    Task<int> SyncFormPermissionsAsync(CancellationToken cancellationToken = default);
}
