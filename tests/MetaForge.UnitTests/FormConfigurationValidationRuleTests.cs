using MetaForge.Application.DTOs;
using MetaForge.Infrastructure.Repositories;
using MetaForge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MetaForge.UnitTests;

public class FormConfigurationValidationRuleTests
{
    [Fact]
    public async Task SaveFormAsync_PersistsValidationRuleJson()
    {
        const string validationJson = """{"rules":[{"type":"maxLength","value":"50"},{"type":"email"}]}""";

        await using var context = CreateContext();
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

        var service = new FormConfigurationService(
            unitOfWork,
            discovery.Object,
            metadata.Object,
            security.Object,
            menuSync.Object);

        var formId = await service.SaveFormAsync(new FormConfigDto
        {
            Code = "customer",
            Name = "Customer",
            EntityName = "Customer",
            TableName = "Customers",
            GroupName = "Master Data",
            FormType = FormType.Master.ToString(),
            IsActive = true,
            Fields =
            [
                new FormFieldConfigDto
                {
                    PropertyName = "Email",
                    Label = "Email",
                    ControlType = ControlType.TextBox,
                    IsVisible = true,
                    ValidationRule = validationJson
                }
            ],
            GridColumns =
            [
                new FormGridColumnConfigDto
                {
                    PropertyName = "Email",
                    Label = "Email",
                    IsVisible = true
                }
            ]
        });

        var loaded = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstAsync(f => f.Id == formId);

        Assert.Equal(validationJson, loaded.Fields.Single().ValidationRule);

        var updatedJson = """{"rules":[{"type":"minLength","value":"3"}]}""";
        await service.SaveFormAsync(new FormConfigDto
        {
            Id = formId,
            Code = "customer",
            Name = "Customer",
            EntityName = "Customer",
            TableName = "Customers",
            GroupName = "Master Data",
            FormType = FormType.Master.ToString(),
            IsActive = true,
            Fields =
            [
                new FormFieldConfigDto
                {
                    PropertyName = "Email",
                    Label = "Email",
                    ControlType = ControlType.TextBox,
                    IsVisible = true,
                    ValidationRule = updatedJson
                }
            ],
            GridColumns =
            [
                new FormGridColumnConfigDto
                {
                    PropertyName = "Email",
                    Label = "Email",
                    IsVisible = true
                }
            ]
        });

        var reloaded = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstAsync(f => f.Id == formId);

        Assert.Equal(updatedJson, reloaded.Fields.Single().ValidationRule);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetaForgeDbContext(options);
    }
}
