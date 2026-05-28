using MetaForge.Application.DTOs;
using MetaForge.Domain.Enums;
using MetaForge.Infrastructure.Repositories;
using MetaForge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MetaForge.UnitTests;

public class FormConfigurationGridActionTests
{
    [Fact]
    public async Task SaveFormAsync_PersistsGridActions()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var formId = await service.SaveFormAsync(new FormConfigDto
        {
            Code = "salesorder",
            Name = "Sales Order",
            EntityName = "SalesOrder",
            TableName = "SalesOrders",
            GroupName = "Transaction",
            FormType = FormType.Master.ToString(),
            IsActive = true,
            Fields =
            [
                new FormFieldConfigDto
                {
                    PropertyName = "OrderNo",
                    Label = "Order No",
                    ControlType = ControlType.TextBox,
                    IsVisible = true
                }
            ],
            GridColumns =
            [
                new FormGridColumnConfigDto
                {
                    PropertyName = "OrderNo",
                    Label = "Order No",
                    IsVisible = true
                }
            ],
            GridActions =
            [
                new FormGridActionConfigDto
                {
                    Code = "approve",
                    Label = "Approve",
                    Icon = "check",
                    Placement = GridActionPlacement.Row,
                    HandlerType = GridActionHandlerType.Api,
                    HandlerTarget = "/api/metaforge/crud/SalesOrder/{id}",
                    HttpMethod = "PUT",
                    RequestBody = """{"Status":"Approved"}""",
                    PermissionAction = PermissionAction.Approve,
                    ConfirmMessage = "Approve this order?",
                    ButtonStyle = "outline-success",
                    IsActive = true
                }
            ]
        });

        var loaded = await context.ForgeForms
            .Include(f => f.GridActions)
            .FirstAsync(f => f.Id == formId);

        var action = Assert.Single(loaded.GridActions);
        Assert.Equal("approve", action.Code);
        Assert.Equal("Approve", action.Label);
        Assert.Equal(GridActionPlacement.Row, action.Placement);
        Assert.Equal(PermissionAction.Approve, action.PermissionAction);
        Assert.Equal("""{"Status":"Approved"}""", action.RequestBody);
    }

    private static FormConfigurationService CreateService(MetaForgeDbContext context)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new ForgeFormRepository(context),
            new ForgeMenuRepository(context));

        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        var metadata = new Mock<IFormMetadataService>();
        var security = new Mock<ISecurityManagementService>();
        var menuSync = new Mock<IMenuSyncService>();

        security.Setup(s => s.SyncFormPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        menuSync.Setup(s => s.SyncFormMenuAsync(It.IsAny<ForgeForm>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadata.Setup(m => m.InvalidateCacheAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new FormConfigurationService(
            unitOfWork,
            discovery.Object,
            metadata.Object,
            security.Object,
            menuSync.Object);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
