namespace MetaForge.UnitTests;

public class LookupServiceTests
{
    [Fact]
    public async Task InvalidateCacheAsync_ForcesReloadAfterEntityChanges()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        var country = new Country { Code = "US", Name = "United States" };
        context.Countries.Add(country);
        context.Regions.Add(new Region { Code = "US-W", Name = "West", Country = country });
        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "Region",
            ValueField = "Id",
            TextField = "Name",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new LookupService(context, new EntityTypeResolver(context), memoryCache);

        var initial = await service.GetLookupItemsAsync("Region", "CountryId", country.Id.ToString());
        Assert.Single(initial);

        context.Regions.Add(new Region { Code = "US-E", Name = "East", CountryId = country.Id });
        await context.SaveChangesAsync();

        var cached = await service.GetLookupItemsAsync("Region", "CountryId", country.Id.ToString());
        Assert.Single(cached);

        await service.InvalidateCacheAsync("Region");

        var refreshed = await service.GetLookupItemsAsync("Region", "CountryId", country.Id.ToString());
        Assert.Equal(2, refreshed.Count);
    }
}

public class GenericCrudServiceLookupCacheTests
{
    [Fact]
    public async Task CreateAsync_InvalidatesLookupCacheForEntity()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        var lookupService = new Mock<ILookupService>();

        var service = new GenericCrudService(
            context,
            new EntityTypeResolver(context),
            Mock.Of<IFormMetadataCache>(),
            lookupService.Object,
            Mock.Of<IDynamicValidationService>(),
            Mock.Of<IAuditService>());

        await service.CreateAsync("Country", new Dictionary<string, object?>
        {
            ["Code"] = "DE",
            ["Name"] = "Germany"
        });

        lookupService.Verify(
            l => l.InvalidateCacheAsync("Country", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
