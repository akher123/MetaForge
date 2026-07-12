namespace MetaForge.Shared.Constants;

/// <summary>
/// System-level security permission codes.
/// </summary>
public static class SecurityPermissions
{
    public const string FormCode = "security";

    public const string ViewUsers = "security.ViewUsers";
    public const string ManageUsers = "security.ManageUsers";
    public const string ViewRoles = "security.ViewRoles";
    public const string ManageRoles = "security.ManageRoles";
    public const string ViewPermissions = "security.ViewPermissions";
    public const string SyncPermissions = "security.SyncPermissions";
    public const string ViewAudit = "security.ViewAudit";

    public static readonly IReadOnlyList<(string Code, string Name, string Action)> All =
    [
        (ViewUsers, "View Users", "ViewUsers"),
        (ManageUsers, "Manage Users", "ManageUsers"),
        (ViewRoles, "View Roles", "ViewRoles"),
        (ManageRoles, "Manage Roles", "ManageRoles"),
        (ViewPermissions, "View Permissions", "ViewPermissions"),
        (SyncPermissions, "Sync Module Permissions", "SyncPermissions"),
        (ViewAudit, "View Audit Log", "ViewAudit")
    ];
}
