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

public class FormHealthCheckServiceTests
{
    [Fact]
    public async Task GetReportAsync_FlagsMissingDetailFormForMasterDetail()
    {
        await using var context = CreateContext();
        var service = CreateService(context, CreateSalesOrderDiscovery());

        context.ForgeForms.Add(new ForgeForm
        {
            Code = "salesorder",
            Name = "Sales Order",
            EntityName = "SalesOrder",
            TableName = "SalesOrders",
            GroupName = "Transaction",
            FormType = FormType.MasterDetailTabular,
            IsActive = true,
            Fields = [new ForgeField { PropertyName = "OrderNo", Label = "Order No", ControlType = ControlType.TextBox, IsVisible = true }],
            GridColumns = [new ForgeGridColumn { PropertyName = "OrderNo", Label = "Order No", IsVisible = true }],
            Relations =
            [
                new ForgeRelation
                {
                    RelationType = RelationType.OneToMany,
                    ParentEntity = "SalesOrder",
                    ChildEntity = "SalesOrderItem",
                    ForeignKey = "SalesOrderId",
                    TabLabel = "Line Items"
                }
            ]
        });
        await context.SaveChangesAsync();

        var report = await service.GetReportAsync();

        var item = Assert.Single(report.Items);
        Assert.Equal(FormHealthStatus.Error, item.Status);
        Assert.Contains(item.Issues, i =>
            i.Category == FormHealthIssueCategories.Relation
            && i.Message.Contains("SalesOrderItem", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReportAsync_FlagsMissingPermissions()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        context.ForgeForms.Add(new ForgeForm
        {
            Code = "product",
            Name = "Product",
            EntityName = "Product",
            TableName = "Products",
            GroupName = "Master Data",
            FormType = FormType.Master,
            IsActive = true,
            Fields = [new ForgeField { PropertyName = "Name", Label = "Name", ControlType = ControlType.TextBox, IsVisible = true }],
            GridColumns = [new ForgeGridColumn { PropertyName = "Name", Label = "Name", IsVisible = true }]
        });
        await context.SaveChangesAsync();

        var report = await service.GetReportAsync();

        var item = Assert.Single(report.Items);
        Assert.Contains(item.Issues, i =>
            i.Category == FormHealthIssueCategories.Permission
            && i.Message.Contains("View", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReportAsync_ReportsUnconfiguredDiscoveredEntity()
    {
        await using var context = CreateContext();
        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        discovery.Setup(d => d.DiscoverAll()).Returns(
        [
            new EntityMetadataDto
            {
                EntityName = "Warehouse",
                TableName = "Warehouses",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                    new EntityPropertyMetadataDto { Name = "Name", ClrType = "System.String" }
                ]
            }
        ]);
        discovery.Setup(d => d.Discover(It.IsAny<string>())).Returns((EntityMetadataDto?)null);

        var service = CreateService(context, discovery);

        var report = await service.GetReportAsync();

        var issue = Assert.Single(report.GlobalIssues);
        Assert.Equal(FormHealthIssueCategories.Discovery, issue.Category);
        Assert.Contains("Warehouse", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Mock<IEntityMetadataDiscoveryService> CreateSalesOrderDiscovery()
    {
        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        discovery.Setup(d => d.DiscoverAll()).Returns([]);
        discovery.Setup(d => d.Discover("SalesOrder")).Returns(new EntityMetadataDto
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
                    RelationType = RelationType.OneToMany,
                    ParentEntity = "SalesOrder",
                    ChildEntity = "SalesOrderItem",
                    ForeignKey = "SalesOrderId"
                }
            ]
        });
        return discovery;
    }

    private static FormHealthCheckService CreateService(
        MetaForgeDbContext context,
        Mock<IEntityMetadataDiscoveryService>? discoveryMock = null)
    {
        var discovery = discoveryMock ?? new Mock<IEntityMetadataDiscoveryService>();
        if (discoveryMock == null)
        {
            discovery.Setup(d => d.DiscoverAll()).Returns([]);
            discovery.Setup(d => d.Discover(It.IsAny<string>())).Returns((EntityMetadataDto?)null);
        }

        var formConfig = new Mock<IFormConfigurationService>();
        formConfig.Setup(s => s.BuildDraftAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string entityName, string groupName, CancellationToken _) => new FormConfigDto
            {
                EntityName = entityName,
                GroupName = groupName,
                Fields = [],
                GridColumns = [],
                Relations = []
            });

        return new FormHealthCheckService(context, discovery.Object, formConfig.Object);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
