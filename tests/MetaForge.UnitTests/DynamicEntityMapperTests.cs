using System.Text.Json;

namespace MetaForge.UnitTests;

public class DynamicEntityMapperTests
{
    [Fact]
    public void ToInt32_ConvertsJsonElementNumber()
    {
        using var doc = JsonDocument.Parse("{\"Id\": 5}");
        var element = doc.RootElement.GetProperty("Id");

        Assert.Equal(5, DynamicEntityMapper.ToInt32(element));
    }

    [Fact]
    public void ToInt32_ConvertsJsonElementString()
    {
        using var doc = JsonDocument.Parse("{\"Id\": \"12\"}");
        var element = doc.RootElement.GetProperty("Id");

        Assert.Equal(12, DynamicEntityMapper.ToInt32(element));
    }

    [Fact]
    public void UpdateEntity_ConvertsJsonElementStringsToTypedProperties()
    {
        var customer = new Customer
        {
            Code = "C001",
            Name = "Original",
            Email = "old@test.com",
            Status = "Active",
            CountryId = 1
        };

        var data = new Dictionary<string, object?>
        {
            ["Name"] = JsonDocument.Parse("\"Updated Name\"").RootElement,
            ["CountryId"] = JsonDocument.Parse("2").RootElement,
            ["Id"] = JsonDocument.Parse("99").RootElement
        };

        DynamicEntityMapper.UpdateEntity(customer, data);

        Assert.Equal("Updated Name", customer.Name);
        Assert.Equal(2, customer.CountryId);
    }

    [Fact]
    public void CreateEntity_ConvertsFormLikePayload()
    {
        var payload = new Dictionary<string, object?>
        {
            ["OrderNo"] = "SO-100",
            ["OrderDate"] = "2026-05-23T10:30:00",
            ["CustomerId"] = "1",
            ["Status"] = "Draft"
        };

        var order = (SalesOrder)DynamicEntityMapper.CreateEntity(typeof(SalesOrder), payload);

        Assert.Equal("SO-100", order.OrderNo);
        Assert.Equal(1, order.CustomerId);
        Assert.Equal("Draft", order.Status);
        Assert.Equal(2026, order.OrderDate.Year);
    }

    [Fact]
    public void NormalizeDictionary_UnwrapsJsonElementPayload()
    {
        using var doc = JsonDocument.Parse("""
            {
              "OrderNo": "SO-200",
              "CustomerId": 3,
              "Quantity": 2,
              "UnitPrice": 19.99
            }
            """);

        var raw = new Dictionary<string, object?>
        {
            ["OrderNo"] = doc.RootElement.GetProperty("OrderNo"),
            ["CustomerId"] = doc.RootElement.GetProperty("CustomerId"),
            ["Quantity"] = doc.RootElement.GetProperty("Quantity"),
            ["UnitPrice"] = doc.RootElement.GetProperty("UnitPrice")
        };

        var normalized = DynamicEntityMapper.NormalizeDictionary(raw);

        Assert.IsType<string>(normalized["OrderNo"]);
        Assert.IsType<int>(normalized["CustomerId"]);
        Assert.IsType<int>(normalized["Quantity"]);
        Assert.IsType<decimal>(normalized["UnitPrice"]);
    }

    [Fact]
    public void CreateEntity_ConvertsNormalizedMasterDetailItem()
    {
        using var doc = JsonDocument.Parse("""
            {
              "ProductId": 1,
              "Quantity": 4,
              "UnitPrice": 29.99
            }
            """);

        var raw = new Dictionary<string, object?>
        {
            ["ProductId"] = doc.RootElement.GetProperty("ProductId"),
            ["Quantity"] = doc.RootElement.GetProperty("Quantity"),
            ["UnitPrice"] = doc.RootElement.GetProperty("UnitPrice")
        };

        var item = (SalesOrderItem)DynamicEntityMapper.CreateEntity(
            typeof(SalesOrderItem),
            DynamicEntityMapper.NormalizeDictionary(raw));

        Assert.Equal(1, item.ProductId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(29.99m, item.UnitPrice);
    }
}
