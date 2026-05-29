using MetaForge.Application.DTOs;

namespace MetaForge.UnitTests;

public class GridDefinitionFilterTests
{
    [Fact]
    public void ApplyPermissions_FiltersActionsByPermission()
    {
        var grid = new GridDefinition
        {
            Actions =
            [
                new GridActionDefinition { Code = "view-action", PermissionAction = "View" },
                new GridActionDefinition { Code = "approve-action", PermissionAction = "Approve" },
                new GridActionDefinition { Code = "default-action", PermissionAction = null }
            ]
        };

        var permissions = new FormPermissionsDto
        {
            CanView = true,
            CanApprove = false,
            GrantedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "View" }
        };

        GridDefinitionFilter.ApplyPermissions(grid, permissions);

        Assert.Equal(2, grid.Actions.Count);
        Assert.Contains(grid.Actions, a => a.Code == "view-action");
        Assert.Contains(grid.Actions, a => a.Code == "default-action");
        Assert.DoesNotContain(grid.Actions, a => a.Code == "approve-action");
    }
}
