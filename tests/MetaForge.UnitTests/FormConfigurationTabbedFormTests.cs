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
