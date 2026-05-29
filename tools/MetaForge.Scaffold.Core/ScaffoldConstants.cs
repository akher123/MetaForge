namespace MetaForge.Scaffold;

internal static class ScaffoldConstants
{
    public static readonly HashSet<string> BlockedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
        "ForgeForms",
        "ForgeFields",
        "ForgeRelations",
        "ForgeGridColumns",
        "ForgeFormActions",
        "ForgeMenus",
        "LookupConfigurations",
        "Users",
        "Roles",
        "Permissions",
        "UserRoles",
        "RolePermissions",
        "AuditLogs"
    };
}
