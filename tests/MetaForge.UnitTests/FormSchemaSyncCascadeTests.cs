using MetaForge.Application.DTOs;
using MetaForge.Application.Interfaces;
using MetaForge.Domain.Enums;
using MetaForge.Domain.Metadata;
using MetaForge.Infrastructure.Persistence;
using MetaForge.Infrastructure.Repositories;
using MetaForge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MetaForge.UnitTests;

public class FormSchemaSyncCascadeTests
{
    [Fact]
    public async Task GetSchemaSyncPreviewAsync_IncludesChildFormsForMasterDetailTabular()
    {
        await using var context = CreateContext();
        var discovery = CreateSalesOrderDiscovery();
        var service = CreateService(context, discovery);

        var masterId = await SaveMasterDetailTabularMasterAsync(service);

        var preview = await service.GetSchemaSyncPreviewAsync(masterId);

        Assert.True(preview.IsCascadeSync);
        Assert.Equal("MasterDetailTabular", preview.ScreenType);
        Assert.Contains(preview.Changes, c => c.Key.StartsWith("SalesOrder|", StringComparison.OrdinalIgnoreCase));

        var itemPreview = Assert.Single(preview.ChildForms, c =>
            c.EntityName.Equals("SalesOrderItem", StringComparison.OrdinalIgnoreCase));
        Assert.True(itemPreview.IsNewForm);
        Assert.Contains(itemPreview.Changes, c =>
            c.Key.StartsWith("SalesOrderItem|", StringComparison.OrdinalIgnoreCase)
            && c.ChangeType == FormSchemaSyncChangeTypes.Add);
    }

    [Fact]
    public async Task ApplySchemaSyncAsync_CreatesMissingDetailFormDuringCascade()
    {
        await using var context = CreateContext();
        var discovery = CreateSalesOrderDiscovery();
        var service = CreateService(context, discovery);

        var masterId = await SaveMasterDetailTabularMasterAsync(service);
        var preview = await service.GetSchemaSyncPreviewAsync(masterId);

        var itemPreview = Assert.Single(preview.ChildForms, c =>
            c.EntityName.Equals("SalesOrderItem", StringComparison.OrdinalIgnoreCase));
        var acceptedKeys = itemPreview.Changes
            .Where(c => c.ChangeType == FormSchemaSyncChangeTypes.Add)
            .Select(c => c.Key)
            .ToList();

        var result = await service.ApplySchemaSyncAsync(masterId, new FormSchemaSyncApplyDto
        {
            AcceptedKeys = acceptedKeys
        });

        Assert.True(result.IsCascadeSync);
        var childResult = Assert.Single(result.ChildForms);
        Assert.True(childResult.WasCreated);
        Assert.Equal("SalesOrderItem", childResult.EntityName);

        var savedDetail = await context.ForgeForms.SingleAsync(f => f.EntityName == "SalesOrderItem");
        Assert.Equal(FormType.Detail, savedDetail.FormType);
        Assert.Contains(savedDetail.Fields, f => f.PropertyName == "ProductId");
    }

    [Fact]
    public async Task GetSchemaSyncPreviewAsync_DoesNotCascadeForMasterForm()
    {
        await using var context = CreateContext();
        var discovery = CreateSalesOrderDiscovery();
        var service = CreateService(context, discovery);

        var masterId = await service.SaveFormAsync(new FormConfigDto
        {
            Code = "product",
            Name = "Product",
            EntityName = "Product",
            TableName = "Products",
            GroupName = "Master Data",
            FormType = FormType.Master.ToString(),
            IsActive = true,
            Fields =
            [
                new FormFieldConfigDto
                {
                    PropertyName = "Name",
                    Label = "Name",
                    ControlType = ControlType.TextBox,
                    IsVisible = true
                }
            ],
            GridColumns =
            [
                new FormGridColumnConfigDto
                {
                    PropertyName = "Name",
                    Label = "Name",
                    IsVisible = true
                }
            ]
        });

        var preview = await service.GetSchemaSyncPreviewAsync(masterId);

        Assert.False(preview.IsCascadeSync);
        Assert.Empty(preview.ChildForms);
        Assert.DoesNotContain(preview.Changes, c => c.Key.Contains('|'));
    }

    private static Mock<IEntityMetadataDiscoveryService> CreateSalesOrderDiscovery()
    {
        var discovery = new Mock<IEntityMetadataDiscoveryService>();

        discovery.Setup(d => d.Discover("SalesOrder"))
            .Returns(new EntityMetadataDto
            {
                EntityName = "SalesOrder",
                TableName = "SalesOrders",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                    new EntityPropertyMetadataDto { Name = "OrderNo", ClrType = "System.String" }
                ],
                Relations =
                [
                    new EntityRelationMetadataDto
                    {
                        RelationType = RelationType.OneToMany.ToString(),
                        ParentEntity = "SalesOrder",
                        ChildEntity = "SalesOrderItem",
                        ForeignKey = "SalesOrderId"
                    }
                ]
            });

        discovery.Setup(d => d.Discover("SalesOrderItem"))
            .Returns(new EntityMetadataDto
            {
                EntityName = "SalesOrderItem",
                TableName = "SalesOrderItems",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                    new EntityPropertyMetadataDto { Name = "SalesOrderId", ClrType = "System.Int32", IsForeignKey = true },
                    new EntityPropertyMetadataDto { Name = "ProductId", ClrType = "System.Int32", IsForeignKey = true },
                    new EntityPropertyMetadataDto { Name = "Quantity", ClrType = "System.Int32" }
                ]
            });

        discovery.Setup(d => d.Discover("Product"))
            .Returns(new EntityMetadataDto
            {
                EntityName = "Product",
                TableName = "Products",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                    new EntityPropertyMetadataDto { Name = "Name", ClrType = "System.String" }
                ]
            });

        discovery.Setup(d => d.DiscoverAll()).Returns([]);

        return discovery;
    }

    private static async Task<int> SaveMasterDetailTabularMasterAsync(FormConfigurationService service)
    {
        return await service.SaveFormAsync(new FormConfigDto
        {
            Code = "salesorder",
            Name = "Sales Order",
            EntityName = "SalesOrder",
            TableName = "SalesOrders",
            GroupName = "Transaction",
            FormType = FormType.MasterDetailTabular.ToString(),
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
            Relations =
            [
                new FormRelationConfigDto
                {
                    RelationType = RelationType.OneToMany.ToString(),
                    ParentEntity = "SalesOrder",
                    ChildEntity = "SalesOrderItem",
                    ForeignKey = "SalesOrderId",
                    TabLabel = "Line Items",
                    DisplayOrder = 0
                }
            ]
        });
    }

    private static FormConfigurationService CreateService(
        MetaForgeDbContext context,
        Mock<IEntityMetadataDiscoveryService> discovery)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new ForgeFormRepository(context),
            new ForgeMenuRepository(context),
            new ForgeReportRepository(context));

        var metadata = new Mock<IFormMetadataService>();
        var security = new Mock<ISecurityManagementService>();
        var menuSync = new Mock<IMenuSyncService>();

        security.Setup(s => s.SyncFormPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        menuSync.Setup(s => s.SyncFormMenuAsync(It.IsAny<ForgeForm>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadata.Setup(m => m.InvalidateCacheAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lookup = new Mock<ILookupService>();
        lookup.Setup(l => l.InvalidateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new FormConfigurationService(
            unitOfWork,
            context,
            discovery.Object,
            metadata.Object,
            security.Object,
            menuSync.Object,
            lookup.Object);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
