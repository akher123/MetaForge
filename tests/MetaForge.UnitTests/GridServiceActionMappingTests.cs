using MetaForge.Infrastructure.Services;

namespace MetaForge.UnitTests;

public class GridServiceActionMappingTests
{
    [Fact]
    public void MapGrid_IncludesActiveGridActions()
    {
        var form = new ForgeForm
        {
            Code = "salesorder",
            Name = "Sales Order",
            EntityName = "SalesOrder",
            GridColumns =
            [
                new ForgeGridColumn { PropertyName = "OrderNo", Label = "Order No", DisplayOrder = 0, IsVisible = true }
            ],
            GridActions =
            [
                new ForgeFormAction
                {
                    Code = "approve",
                    Label = "Approve",
                    Icon = "check",
                    Placement = GridActionPlacement.Row,
                    HandlerType = GridActionHandlerType.Api,
                    HandlerTarget = "/api/metaforge/crud/SalesOrder/{id}",
                    HttpMethod = "PUT",
                    PermissionAction = PermissionAction.Approve,
                    ButtonStyle = "outline-success",
                    DisplayOrder = 0,
                    IsActive = true
                },
                new ForgeFormAction
                {
                    Code = "hidden",
                    Label = "Hidden",
                    HandlerTarget = "/test",
                    DisplayOrder = 1,
                    IsActive = false
                }
            ]
        };

        var grid = GridService.MapGrid(form);

        var action = Assert.Single(grid.Actions);
        Assert.Equal("approve", action.Code);
        Assert.Equal("Approve", action.Label);
        Assert.Equal(GridActionPlacement.Row, action.Placement);
    }
}
