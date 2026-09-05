namespace MetaForge.Application.Common;

/// <summary>
/// Runtime query parameters for executing a configured report.
/// </summary>
public class ReportQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortColumn { get; set; }

    public bool SortDescending { get; set; }

    public string? SearchTerm { get; set; }

    /// <summary>Runtime filter values keyed by report filter property name.</summary>
    public Dictionary<string, string>? FilterValues { get; set; }

    /// <summary>When true, returns all built rows (used for Excel export).</summary>
    public bool ExportAll { get; set; }
}
