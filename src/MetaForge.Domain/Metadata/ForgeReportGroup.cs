namespace MetaForge.Domain.Metadata;

/// <summary>
/// Group-by level for grouped reports.
/// </summary>
public class ForgeReportGroup
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool SortDescending { get; set; }

    public bool ShowSubtotal { get; set; } = true;

    public bool ShowGroupHeader { get; set; } = true;

    public ForgeReport Report { get; set; } = null!;
}
