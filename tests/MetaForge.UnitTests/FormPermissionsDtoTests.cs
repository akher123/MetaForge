using MetaForge.Application.DTOs;
using MetaForge.Domain.Enums;

namespace MetaForge.UnitTests;

public class FormPermissionsDtoTests
{
    [Fact]
    public void Has_UsesGrantedActionsForStandardAndCustomActions()
    {
        var permissions = new FormPermissionsDto
        {
            CanView = false,
            CanApprove = false,
            GrantedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionAction.View,
                "CustomAction"
            }
        };

        Assert.True(permissions.Has(PermissionAction.View));
        Assert.True(permissions.Has("CustomAction"));
        Assert.False(permissions.Has(PermissionAction.Approve));
    }
}
