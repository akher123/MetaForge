namespace MetaForge.UnitTests;

public class MappingAssociationServiceTests
{
    [Fact]
    public async Task EnrichAsync_LoadsRelatedIdsFromJunctionTable()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        var country = new Country { Code = "US", Name = "United States" };
        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var region1 = new Region { Code = "R1", Name = "Region 1", CountryId = country.Id };
        var region2 = new Region { Code = "R2", Name = "Region 2", CountryId = country.Id };
        context.Regions.AddRange(region1, region2);

        var customer = new Customer { Code = "C1", Name = "Customer 1", CountryId = country.Id };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        context.CustomerRegions.AddRange(
            new CustomerRegion { CustomerId = customer.Id, RegionId = region1.Id },
            new CustomerRegion { CustomerId = customer.Id, RegionId = region2.Id });

        var form = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "RegionIds",
                    Label = "Regions",
                    ControlType = ControlType.MultiSelect,
                    LookupEntity = "Region",
                    MappingEntity = "CustomerRegion",
                    MappingParentKey = "CustomerId",
                    MappingRelatedKey = "RegionId"
                }
            ]
        };
        await context.SaveChangesAsync();

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(form);

        var service = new MappingAssociationService(context, cache.Object, new EntityTypeResolver(context));
        var data = new Dictionary<string, object?>();

        await service.EnrichAsync("Customer", data, customer.Id);

        var ids = Assert.IsType<List<int>>(data["RegionIds"]);
        Assert.Equal(2, ids.Count);
        Assert.Contains(region1.Id, ids);
        Assert.Contains(region2.Id, ids);
    }

    [Fact]
    public async Task SyncAsync_ReplacesJunctionRows()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        var country = new Country { Code = "US", Name = "United States" };
        context.Countries.Add(country);
        await context.SaveChangesAsync();

        var region1 = new Region { Code = "R1", Name = "Region 1", CountryId = country.Id };
        var region2 = new Region { Code = "R2", Name = "Region 2", CountryId = country.Id };
        var region3 = new Region { Code = "R3", Name = "Region 3", CountryId = country.Id };
        context.Regions.AddRange(region1, region2, region3);

        var customer = new Customer { Code = "C1", Name = "Customer 1", CountryId = country.Id };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        context.CustomerRegions.Add(new CustomerRegion { CustomerId = customer.Id, RegionId = region1.Id });
        await context.SaveChangesAsync();

        var form = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "RegionIds",
                    Label = "Regions",
                    ControlType = ControlType.MultiSelect,
                    LookupEntity = "Region",
                    MappingEntity = "CustomerRegion",
                    MappingParentKey = "CustomerId",
                    MappingRelatedKey = "RegionId"
                }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(form);

        var service = new MappingAssociationService(context, cache.Object, new EntityTypeResolver(context));
        await service.SyncAsync("Customer", customer.Id, new Dictionary<string, object?>
        {
            ["RegionIds"] = new[] { region2.Id, region3.Id }
        });
        await context.SaveChangesAsync();

        var stored = await context.CustomerRegions
            .Where(cr => cr.CustomerId == customer.Id)
            .Select(cr => cr.RegionId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal([region2.Id, region3.Id], stored);
    }

    [Fact]
    public async Task ValidateAsync_MultiSelectRequired_ThrowsWhenEmpty()
    {
        var module = new ForgeForm
        {
            EntityName = "Customer",
            Fields =
            [
                new ForgeField
                {
                    PropertyName = "RegionIds",
                    Label = "Regions",
                    ControlType = ControlType.MultiSelect,
                    IsRequired = true,
                    LookupEntity = "Region",
                    MappingEntity = "CustomerRegion",
                    MappingParentKey = "CustomerId",
                    MappingRelatedKey = "RegionId"
                }
            ]
        };

        var cache = new Mock<IFormMetadataCache>();
        cache.Setup(c => c.GetByEntityNameAsync("Customer", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new MetaForgeDbContext(options);
        var service = new DynamicValidationService(cache.Object, context, new EntityTypeResolver(context));

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.ValidateAsync("Customer", new Dictionary<string, object?>
            {
                ["RegionIds"] = Array.Empty<int>()
            }));
    }
}
