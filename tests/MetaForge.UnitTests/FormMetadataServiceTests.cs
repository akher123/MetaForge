namespace MetaForge.UnitTests;

public class FormMetadataServiceTests
{
    [Fact]
    public async Task GetFormDefinitionAsync_ReturnsConfiguredFields()
    {
        var module = new ForgeForm
        {
            Id = 1,
            Code = "customer",
            Name = "Customer",
            EntityName = "Customer",
            Fields =
            [
                new ForgeField { PropertyName = "Code", Label = "Code", ControlType = "TextBox", IsRequired = true, DisplayOrder = 0, IsVisible = true }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByCodeAsync("customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var service = new FormMetadataService(cache.Object);

        var result = await service.GetFormDefinitionAsync("customer");

        Assert.NotNull(result);
        Assert.Equal("Customer", result!.EntityName);
        Assert.Single(result.Fields);
        Assert.Equal("Code", result.Fields[0].PropertyName);
    }

    [Fact]
    public async Task GetFormDefinitionAsync_SecondCallUsesCache()
    {
        var module = new ForgeForm
        {
            Code = "customer",
            Name = "Customer",
            EntityName = "Customer",
            Fields = [new ForgeField { PropertyName = "Code", Label = "Code", ControlType = "TextBox", IsVisible = true, DisplayOrder = 0 }]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByCodeAsync("customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var service = new FormMetadataService(cache.Object);

        await service.GetFormDefinitionAsync("customer");
        await service.GetFormDefinitionAsync("customer");

        cache.Verify(c => c.GetByCodeAsync("customer", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

public class FormMetadataCacheTests
{
    [Fact]
    public async Task GetByCodeAsync_LoadsOnce_ThenServedFromMemoryCache()
    {
        var module = new ForgeForm
        {
            Code = "country",
            EntityName = "Country",
            Name = "Country",
            Fields = [],
            Relations = [],
            GridColumns = []
        };

        var uow = new Mock<MetaForge.Application.Interfaces.Repositories.IUnitOfWork>();
        uow.Setup(u => u.Forms.GetByCodeAsync("country", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var options = Microsoft.Extensions.Options.Options.Create(new MetaForge.Application.Configuration.MetadataCacheOptions());
        var cache = new FormMetadataCache(uow.Object, memoryCache, options);

        var first = await cache.GetByCodeAsync("country");
        var second = await cache.GetByCodeAsync("country");
        var byEntity = await cache.GetByEntityNameAsync("Country");

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(first, byEntity);
        uow.Verify(u => u.Forms.GetByCodeAsync("country", It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.Forms.GetByEntityNameAsync("Country", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalidate_RemovesCachedModule()
    {
        var module = new ForgeForm
        {
            Code = "product",
            EntityName = "Product",
            Name = "Product",
            Fields = [],
            Relations = [],
            GridColumns = []
        };

        var uow = new Mock<MetaForge.Application.Interfaces.Repositories.IUnitOfWork>();
        uow.Setup(u => u.Forms.GetByCodeAsync("product", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var options = Microsoft.Extensions.Options.Options.Create(new MetaForge.Application.Configuration.MetadataCacheOptions());
        var cache = new FormMetadataCache(uow.Object, memoryCache, options);

        await cache.GetByCodeAsync("product");
        cache.Invalidate("product", "Product");
        await cache.GetByCodeAsync("product");

        uow.Verify(u => u.Forms.GetByCodeAsync("product", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

public class DynamicValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_RequiredField_ThrowsValidationException()
    {
        var module = new ForgeForm
        {
            EntityName = "Customer",
            Fields = [new ForgeField { PropertyName = "Code", Label = "Code", IsRequired = true }]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MetaForgeDbContext(options);
        var service = new DynamicValidationService(cache.Object, context, new EntityTypeResolver(context));

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.ValidateAsync("Customer", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task ValidateAsync_UniqueRule_ThrowsWhenDuplicateExists()
    {
        const string uniqueRule = """{"rules":[{"type":"unique","message":"Code already exists."}]}""";

        var module = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "Code",
                    Label = "Code",
                    ValidationRule = uniqueRule
                }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MetaForgeDbContext(options);
        context.Customers.Add(new Customer { Code = "C001", Name = "Existing Customer" });
        await context.SaveChangesAsync();

        var service = new DynamicValidationService(cache.Object, context, new EntityTypeResolver(context));

        var ex = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.ValidateAsync("Customer", new Dictionary<string, object?>
            {
                ["Code"] = "C001",
                ["Name"] = "New Customer"
            }));

        Assert.Contains("Code already exists.", ex.Errors.First().ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_UniqueRule_AllowsSameValueWhenEditingSameRecord()
    {
        const string uniqueRule = """{"rules":[{"type":"unique"}]}""";

        var module = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "Code",
                    Label = "Code",
                    ValidationRule = uniqueRule
                }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MetaForgeDbContext(options);
        var existing = new Customer { Code = "C001", Name = "Existing Customer" };
        context.Customers.Add(existing);
        await context.SaveChangesAsync();

        var service = new DynamicValidationService(cache.Object, context, new EntityTypeResolver(context));

        await service.ValidateAsync("Customer", new Dictionary<string, object?>
        {
            ["Id"] = existing.Id,
            ["Code"] = "C001",
            ["Name"] = "Updated Customer"
        });
    }

    [Fact]
    public async Task ValidateAsync_UniqueRule_PassesForNewUniqueValue()
    {
        const string uniqueRule = """{"rules":[{"type":"unique"}]}""";

        var module = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "Code",
                    Label = "Code",
                    ValidationRule = uniqueRule
                }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MetaForgeDbContext(options);
        context.Customers.Add(new Customer { Code = "C001", Name = "Existing Customer" });
        await context.SaveChangesAsync();

        var service = new DynamicValidationService(cache.Object, context, new EntityTypeResolver(context));

        await service.ValidateAsync("Customer", new Dictionary<string, object?>
        {
            ["Code"] = "C002",
            ["Name"] = "New Customer"
        });
    }
}
