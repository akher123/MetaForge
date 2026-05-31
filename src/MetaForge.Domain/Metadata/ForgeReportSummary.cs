namespace MetaForge.Domain.Metadata;

/// <summary>
/// Grand-total or report-level summary row configuration.
/// </summary>
public class ForgeReportSummary
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public ReportAggregateFunction AggregateFunction { get; set; } = ReportAggregateFunction.Sum;

    public int DisplayOrder { get; set; }

    public ForgeReport Report { get; set; } = null!;
}
