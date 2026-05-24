namespace MetaForge.Domain.Security;

/// <summary>
/// Form-scoped permission definition.
/// </summary>
public class Permission
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
