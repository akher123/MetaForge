namespace MetaForge.Domain.Metadata;

/// <summary>
/// Runtime filter parameter for a dynamic report.
/// </summary>
public class ForgeReportFilter
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    public string ControlType { get; set; } = ReportFilterControlType.TextBox;

    /// <summary>Lookup entity name when <see cref="ControlType"/> is Dropdown.</summary>
    public string? LookupEntity { get; set; }

    /// <summary>Comma-separated static options when <see cref="ControlType"/> is Dropdown.</summary>
    public string? Options { get; set; }

    public string? DefaultValue { get; set; }

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }

    public ForgeReport Report { get; set; } = null!;
}
