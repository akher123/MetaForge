using MetaForge.Infrastructure.Reports;

namespace MetaForge.UnitTests;

public class ReportAggregateCalculatorTests
{
    [Fact]
    public void Compute_Count_ReturnsRowCount()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Amount"] = 10m },
            new() { ["Amount"] = 20m }
        };

        var result = ReportAggregateCalculator.Compute(ReportAggregateFunction.Count, rows, "Amount");

        Assert.Equal(2, result);
    }

    [Fact]
    public void Compute_Sum_ReturnsTotal()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Amount"] = 10m },
            new() { ["Amount"] = 25.5m }
        };

        var result = ReportAggregateCalculator.Compute(ReportAggregateFunction.Sum, rows, "Amount");

        Assert.Equal(35.5m, result);
    }

    [Fact]
    public void Compute_Avg_IgnoresNullValues()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Amount"] = 10m },
            new() { ["Amount"] = null },
            new() { ["Amount"] = 30m }
        };

        var result = ReportAggregateCalculator.Compute(ReportAggregateFunction.Avg, rows, "Amount");

        Assert.Equal(20m, result);
    }
}

public class ReportGroupingBuilderTests
{
    [Fact]
    public void BuildGrouped_EmitsHeadersSubtotalsAndGrandTotal()
    {
        var report = new ForgeReport
        {
            ReportType = ReportType.Grouped,
            Groups =
            [
                new ForgeReportGroup
                {
                    PropertyName = "Status",
                    Label = "Status",
                    DisplayOrder = 0,
                    ShowGroupHeader = true,
                    ShowSubtotal = true
                }
            ],
            Columns =
            [
                new ForgeReportColumn { PropertyName = "OrderNo", Label = "Order No", ColumnRole = ReportColumnRole.Detail, IsVisible = true },
                new ForgeReportColumn { PropertyName = "Id", Label = "Count", ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Count, IsVisible = true }
            ],
            Summaries =
            [
                new ForgeReportSummary { PropertyName = "Id", AggregateFunction = ReportAggregateFunction.Count, DisplayOrder = 0 }
            ]
        };

        var detailRows = new List<Dictionary<string, object?>>
        {
            new() { ["Status"] = "Open", ["OrderNo"] = "SO-1", ["Id"] = 1 },
            new() { ["Status"] = "Open", ["OrderNo"] = "SO-2", ["Id"] = 2 },
            new() { ["Status"] = "Closed", ["OrderNo"] = "SO-3", ["Id"] = 3 }
        };

        var displayColumns = new List<ReportColumnDefinitionDto>
        {
            new() { PropertyName = "OrderNo", Label = "Order No", IsVisible = true },
            new() { PropertyName = "Id", Label = "Count", IsVisible = true }
        };

        var aggregateColumns = report.Columns.Where(c => c.ColumnRole == ReportColumnRole.Aggregate).ToList();
        var result = ReportGroupingBuilder.BuildGrouped(report, detailRows, displayColumns, aggregateColumns, report.Summaries.ToList());

        Assert.Contains(result.Rows, r => r.RowType == ReportRowTypes.GroupHeader && r.Label!.Contains("Open"));
        Assert.Contains(result.Rows, r => r.RowType == ReportRowTypes.GroupSubtotal);
        Assert.Contains(result.Rows, r => r.RowType == ReportRowTypes.Detail);
        Assert.Contains(result.Rows, r => r.RowType == ReportRowTypes.GrandTotal);
        Assert.Equal(3, result.DetailCount);
    }

    [Fact]
    public void BuildSummary_GroupsByStatus()
    {
        var report = new ForgeReport
        {
            ReportType = ReportType.Summary,
            Groups =
            [
                new ForgeReportGroup { PropertyName = "Status", Label = "Status", DisplayOrder = 0 }
            ],
            Columns =
            [
                new ForgeReportColumn { PropertyName = "Status", Label = "Status", ColumnRole = ReportColumnRole.Detail, IsVisible = true },
                new ForgeReportColumn { PropertyName = "Id", Label = "Count", ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Count, IsVisible = true }
            ],
            Summaries =
            [
                new ForgeReportSummary { PropertyName = "Id", AggregateFunction = ReportAggregateFunction.Count, DisplayOrder = 0 }
            ]
        };

        var detailRows = new List<Dictionary<string, object?>>
        {
            new() { ["Status"] = "Active", ["Id"] = 1 },
            new() { ["Status"] = "Active", ["Id"] = 2 },
            new() { ["Status"] = "Inactive", ["Id"] = 3 }
        };

        var displayColumns = new List<ReportColumnDefinitionDto>
        {
            new() { PropertyName = "Status", Label = "Status", IsVisible = true },
            new() { PropertyName = "Id", Label = "Count", IsVisible = true }
        };

        var aggregateColumns = report.Columns.Where(c => c.ColumnRole == ReportColumnRole.Aggregate).ToList();
        var result = ReportGroupingBuilder.BuildSummary(report, detailRows, displayColumns, aggregateColumns, report.Summaries.ToList());

        Assert.Equal(2, result.Rows.Count(r => r.RowType == ReportRowTypes.Summary));
        Assert.Contains(result.Rows, r => r.RowType == ReportRowTypes.GrandTotal);
    }
}
