namespace MetaForge.Domain.Metadata;

/// <summary>
/// Column configuration for dynamic reports.
/// </summary>
public class ForgeReportColumn
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public ReportColumnRole ColumnRole { get; set; } = ReportColumnRole.Detail;

    public ReportAggregateFunction AggregateFunction { get; set; } = ReportAggregateFunction.None;

    public string? DisplayFormat { get; set; }

    public string? Formula { get; set; }

    public ForgeReport Report { get; set; } = null!;
}
