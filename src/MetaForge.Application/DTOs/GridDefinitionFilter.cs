namespace MetaForge.Application.DTOs;

/// <summary>
/// Filters grid actions by the current user's form permissions.
/// </summary>
public static class GridDefinitionFilter
{
    public static void ApplyPermissions(GridDefinition grid, FormPermissionsDto permissions)
    {
        grid.Actions = grid.Actions
            .Where(a => IsAllowed(a, permissions))
            .ToList();
    }

    private static bool IsAllowed(GridActionDefinition action, FormPermissionsDto permissions)
    {
        if (string.IsNullOrWhiteSpace(action.PermissionAction))
            return permissions.CanView;

        return permissions.Has(action.PermissionAction);
    }
}
