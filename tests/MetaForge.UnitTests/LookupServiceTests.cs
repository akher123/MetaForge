using MetaForge.Domain.Features;

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

    [Fact]
    public async Task SearchLookupItemsAsync_FiltersBySearchTermAndPaginates()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        context.Countries.AddRange(
            new Country { Code = "US", Name = "United States" },
            new Country { Code = "CA", Name = "Canada" },
            new Country { Code = "MX", Name = "Mexico" });
        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "Country",
            ValueField = "Id",
            TextField = "Name",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));

        var page = await service.SearchLookupItemsAsync("Country", "a", skip: 0, take: 1);
        Assert.Single(page.Items);
        Assert.True(page.HasMore);

        var canada = await service.SearchLookupItemsAsync("Country", "Canada");
        Assert.Single(canada.Items);
        Assert.Equal("Canada", canada.Items[0].Text);
    }

    [Fact]
    public async Task GetLookupItemByValueAsync_ReturnsMatchingItem()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        var country = new Country { Code = "US", Name = "United States" };
        context.Countries.Add(country);
        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "Country",
            ValueField = "Id",
            TextField = "Name",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));
        var item = await service.GetLookupItemByValueAsync("Country", country.Id.ToString());

        Assert.NotNull(item);
        Assert.Equal("United States", item!.Text);
    }

    [Fact]
    public async Task GetLookupItemsAsync_CapsResultsForLargeDatasets()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        for (var i = 1; i <= 150; i++)
        {
            context.Products.Add(new Product { Code = $"P{i:D3}", Name = $"Product {i}", UnitPrice = 1m });
        }

        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "Product",
            ValueField = "Id",
            TextField = "Name",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));
        var items = await service.GetLookupItemsAsync("Product");

        Assert.True(items.Count <= 100);
    }

    [Fact]
    public async Task SearchLookupItemsAsync_UsesNavigationPropertyDisplayText_WhenFilteredByDriver()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);

        var driver = new Driver
        {
            EmployeeCode = "D001",
            FirstName = "Jane",
            LastName = "Driver",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        var vehicle = new Vehicle
        {
            VehicleNumber = "VH-001",
            Name = "Fleet Truck",
            VehicleTypeId = 1,
            VehicleMakeId = 1,
            VehicleModelId = 1,
            VehicleStatusId = 1,
            CurrentOdometer = 0,
            IsDeleted = false
        };

        context.Set<Driver>().Add(driver);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        context.Set<VehicleAssignment>().Add(new VehicleAssignment
        {
            DriverId = driver.Id,
            VehicleId = vehicle.Id,
            AssignedDate = DateTime.UtcNow,
            Vehicle = vehicle
        });
        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "VehicleAssignment",
            ValueField = "VehicleId",
            TextField = "Vehicle.VehicleNumber",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));

        var results = await service.SearchLookupItemsAsync(
            "VehicleAssignment",
            filterField: "DriverId",
            filterValue: driver.Id.ToString());

        Assert.Single(results.Items);
        Assert.Equal(vehicle.Id.ToString(), results.Items[0].Value);
        Assert.Equal("VH-001", results.Items[0].Text);

        var selected = await service.GetLookupItemByValueAsync("VehicleAssignment", vehicle.Id.ToString());
        Assert.NotNull(selected);
        Assert.Equal("VH-001", selected!.Text);
    }

    [Fact]
    public async Task SearchLookupItemsAsync_DeduplicatesByValueField()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);

        var driver = new Driver
        {
            EmployeeCode = "D002",
            FirstName = "John",
            LastName = "Driver",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        var vehicle = new Vehicle
        {
            VehicleNumber = "VH-002",
            VehicleTypeId = 1,
            VehicleMakeId = 1,
            VehicleModelId = 1,
            VehicleStatusId = 1,
            CurrentOdometer = 0,
            IsDeleted = false
        };

        context.Set<Driver>().Add(driver);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        context.Set<VehicleAssignment>().AddRange(
            new VehicleAssignment
            {
                DriverId = driver.Id,
                VehicleId = vehicle.Id,
                AssignedDate = DateTime.UtcNow.AddDays(-10),
                AssignmentReason = "Initial",
                Vehicle = vehicle
            },
            new VehicleAssignment
            {
                DriverId = driver.Id,
                VehicleId = vehicle.Id,
                AssignedDate = DateTime.UtcNow,
                AssignmentReason = "Renewed",
                Vehicle = vehicle
            });
        context.LookupConfigurations.Add(new LookupConfiguration
        {
            EntityName = "VehicleAssignment",
            ValueField = "VehicleId",
            TextField = "Vehicle.VehicleNumber",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));
        var results = await service.SearchLookupItemsAsync(
            "VehicleAssignment",
            filterField: "DriverId",
            filterValue: driver.Id.ToString());

        Assert.Single(results.Items);
        Assert.Equal(vehicle.Id.ToString(), results.Items[0].Value);
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
            Mock.Of<IAuditService>(),
            Mock.Of<IMappingAssociationService>());

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
