namespace MetaForge.UnitTests.TestEntities;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? CountryId { get; set; }
}

public class Country
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Region
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public Country? Country { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CustomerRegion
{
    public int CustomerId { get; set; }
    public int RegionId { get; set; }
}

public class SalesOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<SalesOrderItem> Items { get; set; } = [];
}

public class SalesOrderItem
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public SalesOrder? SalesOrder { get; set; }
}

public class VehicleMake
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Vehicle
{
    public int Id { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public class VehicleAssignment
{
    public int Id { get; set; }
    public Vehicle? Vehicle { get; set; }
}
