namespace MetaForge.UnitTests;

public class GenericCrudServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsPagedCountryData()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new MetaForgeDbContext(options);
        context.Countries.AddRange(
            new Country { Code = "US", Name = "United States" },
            new Country { Code = "UK", Name = "United Kingdom" });
        context.ForgeForms.Add(new ForgeForm
        {
            Code = "country",
            Name = "Country",
            EntityName = "Country",
            TableName = "Countries",
            GridColumns =
            [
                new ForgeGridColumn { PropertyName = "Code", Label = "Code", DisplayOrder = 0, IsVisible = true, IsSearchable = true },
                new ForgeGridColumn { PropertyName = "Name", Label = "Name", DisplayOrder = 1, IsVisible = true, IsSearchable = true }
            ]
        });
        await context.SaveChangesAsync();

        var module = context.ForgeForms.Include(m => m.GridColumns).First();
        var moduleCache = new Mock<IFormMetadataCache>();
        moduleCache.Setup(c => c.GetByEntityNameAsync("Country", It.IsAny<CancellationToken>())).ReturnsAsync(module);

        var service = new GenericCrudService(
            context,
            new EntityTypeResolver(context),
            moduleCache.Object,
            Mock.Of<ILookupService>(),
            Mock.Of<IDynamicValidationService>(),
            Mock.Of<IAuditService>());

        var result = await service.GetAllAsync(new GridQueryRequest
        {
            Entity = "Country",
            Page = 1,
            PageSize = 25,
            SortColumn = "Code"
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item["Code"]?.ToString() == "UK");
        Assert.Contains(result.Items, item => item["Code"]?.ToString() == "US");
        Assert.True(result.Items[0].ContainsKey("Id"));
        Assert.NotNull(result.Items[0]["Id"]);
    }
}
