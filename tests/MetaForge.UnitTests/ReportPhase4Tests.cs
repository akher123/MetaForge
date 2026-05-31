using MetaForge.Infrastructure.Reports;
using MetaForge.Infrastructure.Services;

namespace MetaForge.UnitTests;

public class ReportFormulaEvaluatorTests
{
    [Fact]
    public void Evaluate_MultipliesFields()
    {
        var row = new Dictionary<string, object?>
        {
            ["Quantity"] = 3,
            ["UnitPrice"] = 12.5m
        };

        var result = ReportFormulaEvaluator.Evaluate("{Quantity} * {UnitPrice}", row);

        Assert.Equal(37.5m, result);
    }

    [Fact]
    public void Evaluate_ReturnsNullForEmptyFormula()
    {
        var row = new Dictionary<string, object?> { ["Quantity"] = 1m };
        Assert.Null(ReportFormulaEvaluator.Evaluate(null, row));
    }

    [Fact]
    public void ExtractDependencies_ParsesTokens()
    {
        var deps = ReportFormulaEvaluator.ExtractDependencies("{Quantity} * {UnitPrice} + {TaxAmount}");

        Assert.Contains("Quantity", deps);
        Assert.Contains("UnitPrice", deps);
        Assert.Contains("TaxAmount", deps);
    }

    [Fact]
    public void ApplyCalculations_SetsCalculatedColumnValues()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Quantity"] = 2m, ["UnitPrice"] = 10m },
            new() { ["Quantity"] = 5m, ["UnitPrice"] = 4m }
        };

        ReportFormulaEvaluator.ApplyCalculations(rows,
        [
            new ReportColumnDefinitionDto
            {
                PropertyName = "LineTotal",
                Formula = "{Quantity} * {UnitPrice}"
            }
        ]);

        Assert.Equal(20m, rows[0]["LineTotal"]);
        Assert.Equal(20m, rows[1]["LineTotal"]);
    }

    [Fact]
    public void GetSourcePropertyColumns_IncludesFormulaDependencies()
    {
        var report = new ForgeReport
        {
            Columns =
            [
                new ForgeReportColumn { PropertyName = "Quantity", ColumnRole = ReportColumnRole.Detail },
                new ForgeReportColumn { PropertyName = "UnitPrice", ColumnRole = ReportColumnRole.Detail },
                new ForgeReportColumn { PropertyName = "LineTotal", ColumnRole = ReportColumnRole.Calculated, Formula = "{Quantity} * {UnitPrice}" }
            ]
        };

        var source = ReportService.GetSourcePropertyColumns(report);

        Assert.Contains("Quantity", source);
        Assert.Contains("UnitPrice", source);
        Assert.DoesNotContain("LineTotal", source);
    }
}

public class ReportExportTokenFormatterTests
{
    [Fact]
    public void Format_ReplacesTitleAndDateTokens()
    {
        var layout = new ReportExportLayoutDto { Title = "Sales Report" };
        var text = ReportExportTokenFormatter.Format("{Title} - {Date}", layout);

        Assert.StartsWith("Sales Report - ", text);
        Assert.DoesNotContain("{Title}", text);
    }

    [Fact]
    public void HasHeader_ReturnsTrueWhenAnyHeaderFieldSet()
    {
        var layout = new ReportExportLayoutDto { HeaderLeft = "Company" };
        Assert.True(ReportExportTokenFormatter.HasHeader(layout));
    }
}

public class ReportPdfExporterTests
{
    [Fact]
    public void Export_ReturnsNonEmptyPdfBytes()
    {
        var result = new ReportResultDto
        {
            ReportType = "Tabular",
            Columns =
            [
                new ReportColumnDefinitionDto { PropertyName = "Name", Label = "Name", IsVisible = true },
                new ReportColumnDefinitionDto { PropertyName = "Amount", Label = "Amount", IsVisible = true, DisplayFormat = "N2" }
            ],
            Rows =
            [
                new ReportRowDto
                {
                    RowType = ReportRowTypes.Detail,
                    Values = new Dictionary<string, object?> { ["Name"] = "Alpha", ["Amount"] = 10.5m }
                }
            ]
        };

        var layout = new ReportExportLayoutDto
        {
            Title = "Sample Report",
            ShowTitleUnderline = true,
            ShowSignatureBlock = true,
            Signatures =
            [
                new ReportSignatureLineDto { Label = "Prepared By", DisplayOrder = 0 },
                new ReportSignatureLineDto { Label = "Approved By", DisplayOrder = 1 }
            ]
        };

        var bytes = ReportPdfExporter.Export(result, layout);

        Assert.NotEmpty(bytes);
        Assert.Equal('%', (char)bytes[0]);
        Assert.Equal('P', (char)bytes[1]);
        Assert.Equal('D', (char)bytes[2]);
        Assert.Equal('F', (char)bytes[3]);
    }
}
