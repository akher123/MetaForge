namespace MetaForge.UnitTests;

public class GridDisplayEnricherTests
{
    [Fact]
    public async Task EnrichAsync_ReplacesLookupIdWithDisplayText()
    {
        var lookupService = new Mock<ILookupService>();
        lookupService
            .Setup(s => s.ResolveLookupTextsAsync("Customer", It.Is<IEnumerable<string>>(v => v.Contains("1")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Acme Corp"
            });

        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = 10,
                ["OrderNo"] = "SO-001",
                ["CustomerId"] = 1
            }
        };

        var columns = new List<GridColumnDefinition>
        {
            new() { PropertyName = "OrderNo", Label = "Order No" },
            new() { PropertyName = "CustomerId", Label = "Customer", LookupEntity = "Customer" }
        };

        await GridDisplayEnricher.EnrichAsync(rows, columns, lookupService.Object);

        Assert.Equal("Acme Corp", rows[0]["CustomerId"]);
        Assert.Equal(10, rows[0]["Id"]);
    }
}
