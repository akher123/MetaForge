namespace MetaForge.UnitTests;

public class GridDisplayEnricherTests
{
    [Fact]
    public async Task EnrichAsync_ReplacesLookupIdWithDisplayText()
    {
        var lookupService = new Mock<ILookupService>();
        lookupService
            .Setup(s => s.GetLookupItemsAsync("Customer", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LookupItemDto>
            {
                new() { Value = "1", Text = "Acme Corp" },
                new() { Value = "2", Text = "Globex" }
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
