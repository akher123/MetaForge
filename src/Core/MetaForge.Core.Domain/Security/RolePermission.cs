namespace MetaForge.Domain.Security;

/// <summary>
/// Join entity between roles and permissions.
/// </summary>
public class RolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    public Role Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
