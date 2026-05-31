using MetaForge.Domain.Business;
using MetaForge.Domain.Metadata;
using MetaForge.Infrastructure.Reports;

namespace MetaForge.UnitTests;

public class ReportPropertyPathResolverTests
{
    [Fact]
    public void IsValidPath_AcceptsRootScalarProperty()
    {
        Assert.True(ReportPropertyPathResolver.IsValidPath(typeof(SalesOrderItem), "Quantity"));
    }

    [Fact]
    public void IsValidPath_AcceptsNestedNavigationProperty()
    {
        Assert.True(ReportPropertyPathResolver.IsValidPath(typeof(SalesOrderItem), "SalesOrder.OrderNo"));
        Assert.True(ReportPropertyPathResolver.IsValidPath(typeof(SalesOrderItem), "SalesOrder.Customer.Name"));
    }

    [Fact]
    public void IsValidPath_RejectsCollectionNavigation()
    {
        Assert.False(ReportPropertyPathResolver.IsValidPath(typeof(SalesOrderItem), "SalesOrder.Items.Quantity"));
    }

    [Fact]
    public void GetMinimalIncludePaths_DedupesNestedIncludes()
    {
        var includes = ReportPropertyPathResolver.GetMinimalIncludePaths([
            "SalesOrder.OrderNo",
            "SalesOrder.Customer.Name",
            "Product.Name"
        ]);

        Assert.Contains("SalesOrder", includes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("SalesOrder.Customer", includes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Product", includes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverPaths_IncludesRelatedScalars()
    {
        var paths = ReportPropertyPathResolver.DiscoverPaths(typeof(SalesOrderItem));

        Assert.Contains(paths, p => p.Path == "Quantity");
        Assert.Contains(paths, p => p.Path == "SalesOrder.OrderNo");
        Assert.Contains(paths, p => p.Path == "SalesOrder.Customer.Name");
        Assert.Contains(paths, p => p.Path == "Product.Name");
    }
}

public class ReportDynamicQueryTests
{
    [Fact]
    public void ApplyFilters_FiltersByNestedNavigationProperty()
    {
        var data = new List<SalesOrderItem>
        {
            new()
            {
                Id = 1,
                Quantity = 2,
                SalesOrder = new SalesOrder { OrderNo = "SO-001", Status = "Draft" }
            },
            new()
            {
                Id = 2,
                Quantity = 1,
                SalesOrder = new SalesOrder { OrderNo = "SO-002", Status = "Approved" }
            }
        }.AsQueryable();

        var filtered = ReportDynamicQuery.ApplyFilters(data, new Dictionary<string, string>
        {
            ["SalesOrder.Status"] = "Draft"
        }).ToList();

        Assert.Single(filtered);
        Assert.Equal(2, filtered[0].Quantity);
    }

    [Fact]
    public void ApplySort_SortsByNestedProperty()
    {
        var data = new List<SalesOrderItem>
        {
            new() { Id = 1, SalesOrder = new SalesOrder { OrderNo = "SO-B" } },
            new() { Id = 2, SalesOrder = new SalesOrder { OrderNo = "SO-A" } }
        }.AsQueryable();

        var sorted = ReportDynamicQuery.ApplySort(data, "SalesOrder.OrderNo", descending: false).ToList();

        Assert.Equal("SO-A", sorted[0].SalesOrder.OrderNo);
        Assert.Equal("SO-B", sorted[1].SalesOrder.OrderNo);
    }

    [Fact]
    public void ReportNavigationMapper_FlattensNestedPaths()
    {
        var item = new SalesOrderItem
        {
            Quantity = 3,
            UnitPrice = 10m,
            SalesOrder = new SalesOrder
            {
                OrderNo = "SO-001",
                Customer = new Customer { Name = "Contoso Ltd" }
            },
            Product = new Product { Name = "Widget A" }
        };

        var row = ReportNavigationMapper.ToDictionary(item,
        [
            "SalesOrder.OrderNo",
            "SalesOrder.Customer.Name",
            "Product.Name",
            "Quantity",
            "UnitPrice"
        ]);

        Assert.Equal("SO-001", row["SalesOrder.OrderNo"]);
        Assert.Equal("Contoso Ltd", row["SalesOrder.Customer.Name"]);
        Assert.Equal("Widget A", row["Product.Name"]);
        Assert.Equal(3, row["Quantity"]);
    }
}

public class ReportQueryPlannerTests
{
    [Fact]
    public void Create_IncludesNavigationDependenciesFromColumnsAndFilters()
    {
        var report = new ForgeReport
        {
            EntityName = "SalesOrderItem",
            Columns =
            [
                new ForgeReportColumn { PropertyName = "SalesOrder.OrderNo", ColumnRole = ReportColumnRole.Detail },
                new ForgeReportColumn { PropertyName = "Quantity", ColumnRole = ReportColumnRole.Detail }
            ],
            Filters =
            [
                new ForgeReportFilter { PropertyName = "SalesOrder.Customer.Name" }
            ]
        };

        var plan = ReportQueryPlanner.Create<SalesOrderItem>(report, new ReportQueryRequest());

        Assert.Contains("SalesOrder.OrderNo", plan.PropertyPaths);
        Assert.Contains("SalesOrder.Customer", plan.IncludePaths, StringComparer.OrdinalIgnoreCase);
    }
}

public class ReportFilterHelperControlTests
{
    [Fact]
    public void NormalizeControlType_AcceptsAutocomplete()
    {
        Assert.Equal(ReportFilterControlType.Autocomplete, ReportFilterHelper.NormalizeControlType("Autocomplete"));
    }

    [Fact]
    public void InferForProperty_UsesAutocompleteForForeignKey()
    {
        var inferred = ReportFilterHelper.InferForProperty("CustomerId", "System.Int32", isForeignKey: true);

        Assert.Equal(ReportFilterControlType.Autocomplete, inferred.ControlType);
        Assert.Equal("Customer", inferred.LookupEntity);
    }
}
