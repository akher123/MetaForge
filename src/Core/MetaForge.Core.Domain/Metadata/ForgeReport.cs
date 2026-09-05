namespace MetaForge.Domain.Metadata;

/// <summary>
/// Configures a metadata-driven dynamic report for a feature entity.
/// </summary>
public class ForgeReport
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? GroupName { get; set; }

    public ReportType ReportType { get; set; } = ReportType.Tabular;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    /// <summary>Optional export title override; falls back to <see cref="Name"/>.</summary>
    public string? ExportTitle { get; set; }

    public bool ShowTitleUnderline { get; set; } = true;

    public bool ShowSignatureBlock { get; set; }

    public string? HeaderLeft { get; set; }

    public string? HeaderCenter { get; set; }

    public string? HeaderRight { get; set; }

    public string? FooterLeft { get; set; }

    public string? FooterCenter { get; set; }

    public string? FooterRight { get; set; }

    public bool ShowPageNumbers { get; set; } = true;

    public bool ShowGeneratedTimestamp { get; set; } = true;

    public ICollection<ForgeReportColumn> Columns { get; set; } = [];

    public ICollection<ForgeReportFilter> Filters { get; set; } = [];

    public ICollection<ForgeReportGroup> Groups { get; set; } = [];

    public ICollection<ForgeReportSummary> Summaries { get; set; } = [];

    public ICollection<ForgeReportSignature> Signatures { get; set; } = [];
}
