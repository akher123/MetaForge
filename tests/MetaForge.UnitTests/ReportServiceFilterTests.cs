using MetaForge.Infrastructure.Services;

namespace MetaForge.UnitTests;

public class ReportServiceFilterTests
{
    [Fact]
    public void BuildFilterDictionary_MapsContainsOperatorToSuffixKey()
    {
        var report = new ForgeReport
        {
            Filters =
            [
                new ForgeReportFilter
                {
                    PropertyName = "Name",
                    Operator = FilterOperator.Contains
                }
            ]
        };

        var filters = ReportService.BuildFilterDictionary(report, new Dictionary<string, string>
        {
            ["Name"] = "Contoso"
        });

        Assert.True(filters.TryGetValue("Name__contains", out var value));
        Assert.Equal("Contoso", value);
    }

    [Fact]
    public void BuildFilterDictionary_MapsDateRangeToBetweenOrBounds()
    {
        var report = new ForgeReport
        {
            Filters =
            [
                new ForgeReportFilter
                {
                    PropertyName = "OrderDate",
                    ControlType = ReportFilterControlType.DateRange,
                    Operator = FilterOperator.Between
                }
            ]
        };

        var both = ReportService.BuildFilterDictionary(report, new Dictionary<string, string>
        {
            ["OrderDate"] = "2026-01-01|2026-01-31"
        });
        Assert.True(both.TryGetValue("OrderDate__between", out var range));
        Assert.Equal("2026-01-01|2026-01-31", range);

        var fromOnly = ReportService.BuildFilterDictionary(report, new Dictionary<string, string>
        {
            ["OrderDate"] = "2026-01-01|"
        });
        Assert.True(fromOnly.TryGetValue("OrderDate__gte", out var from));
        Assert.Equal("2026-01-01", from);
    }
}
