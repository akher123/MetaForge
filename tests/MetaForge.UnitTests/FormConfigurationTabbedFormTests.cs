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

public class FormConfigurationTabbedFormTests
{
    [Fact]
    public async Task SaveScreenAsync_PersistsTabbedFormType()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var formId = await service.SaveScreenAsync(new FormBuilderSaveDto
        {
            ScreenType = "Tabbed",
            Master = new FormConfigDto
            {
                Code = "customer",
                Name = "Customer",
                EntityName = "Customer",
                TableName = "Customers",
                GroupName = "Master Data",
                IsActive = true,
                Fields =
                [
                    new FormFieldConfigDto
                    {
                        PropertyName = "Name",
                        Label = "Name",
                        ControlType = ControlType.TextBox,
                        SectionName = "General",
                        IsVisible = true
                    },
                    new FormFieldConfigDto
                    {
                        PropertyName = "City",
                        Label = "City",
                        ControlType = ControlType.TextBox,
                        SectionName = "Address",
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
            }
        });

        var loaded = await context.ForgeForms.FirstAsync(f => f.Id == formId);
        Assert.Equal(FormType.Tabbed, loaded.FormType);
    }

    [Fact]
    public async Task GetScreenAsync_ReturnsTabbedScreenType()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var formId = await service.SaveScreenAsync(new FormBuilderSaveDto
        {
            ScreenType = "Tabbed",
            Master = new FormConfigDto
            {
                Code = "supplier",
                Name = "Supplier",
                EntityName = "Supplier",
                TableName = "Suppliers",
                GroupName = "Master Data",
                IsActive = true,
                Fields =
                [
                    new FormFieldConfigDto
                    {
                        PropertyName = "Name",
                        Label = "Name",
                        ControlType = ControlType.TextBox,
                        SectionName = "General",
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
            }
        });

        var screen = await service.GetScreenAsync(formId);
        Assert.Equal("Tabbed", screen.ScreenType);
        Assert.Equal(FormType.Tabbed.ToString(), screen.Master.FormType);
    }

    [Fact]
    public async Task SaveFormAsync_PersistsLookupDisplayFieldConfiguration()
    {
        await using var context = CreateContext();
        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        discovery.Setup(d => d.Discover("Vehicle"))
            .Returns(new EntityMetadataDto
            {
                EntityName = "Vehicle",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                    new EntityPropertyMetadataDto { Name = "VehicleNumber", ClrType = "System.String" }
                ]
            });

        var service = CreateService(context, discovery.Object);

        await service.SaveFormAsync(new FormConfigDto
        {
            Code = "fueltransaction",
            Name = "Fuel Transaction",
            EntityName = "FuelTransaction",
            TableName = "FuelTransactions",
            GroupName = "Transaction",
            FormType = FormType.Master.ToString(),
            IsActive = true,
            Fields =
            [
                new FormFieldConfigDto
                {
                    PropertyName = "VehicleId",
                    Label = "Vehicle",
                    ControlType = ControlType.Autocomplete,
                    LookupEntity = "Vehicle",
                    LookupTextField = "VehicleNumber",
                    LookupValueField = "Id",
                    IsVisible = true
                }
            ],
            GridColumns =
            [
                new FormGridColumnConfigDto
                {
                    PropertyName = "VehicleId",
                    Label = "Vehicle",
                    IsVisible = true
                }
            ]
        });

        var lookupConfig = await context.LookupConfigurations.SingleAsync(c => c.EntityName == "Vehicle");
        Assert.Equal("VehicleNumber", lookupConfig.TextField);
        Assert.Equal("Id", lookupConfig.ValueField);
    }

    [Fact]
    public async Task SaveFormAsync_AllowsMasterAndTreeForSameEntity()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var treeId = await service.SaveFormAsync(new FormConfigDto
        {
            Code = "locationtree",
            Name = "Location Tree",
            EntityName = "Country",
            TableName = "Countries",
            GroupName = "Master Data",
            FormType = FormType.TreeViewMultiTable.ToString(),
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

        var masterId = await service.SaveFormAsync(new FormConfigDto
        {
            Code = "country",
            Name = "Country",
            EntityName = "Country",
            TableName = "Countries",
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

        var updatedMasterId = await service.SaveFormAsync(new FormConfigDto
        {
            Id = masterId,
            Code = "country",
            Name = "Country",
            EntityName = "Country",
            TableName = "Countries",
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
                },
                new FormFieldConfigDto
                {
                    PropertyName = "Code",
                    Label = "Code",
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
                },
                new FormGridColumnConfigDto
                {
                    PropertyName = "Code",
                    Label = "Code",
                    IsVisible = true
                }
            ]
        });

        Assert.NotEqual(treeId, masterId);
        Assert.Equal(masterId, updatedMasterId);
        Assert.Equal(2, await context.ForgeForms.CountAsync(f => f.EntityName == "Country"));
    }

    private static FormConfigurationService CreateService(
        MetaForgeDbContext context,
        IEntityMetadataDiscoveryService? discoveryService = null)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new ForgeFormRepository(context),
            new ForgeMenuRepository(context),
            new ForgeReportRepository(context));

        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        if (discoveryService != null)
            discovery.Setup(d => d.Discover(It.IsAny<string>()))
                .Returns((string name) => discoveryService.Discover(name));
        else
            discovery.Setup(d => d.Discover(It.IsAny<string>())).Returns((EntityMetadataDto?)null);

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
            discoveryService ?? discovery.Object,
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
