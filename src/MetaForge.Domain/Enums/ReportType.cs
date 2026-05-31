namespace MetaForge.Domain.Enums;

/// <summary>
/// Layout style for dynamic reports.
/// </summary>
public enum ReportType
{
    /// <summary>Flat list of detail rows.</summary>
    Tabular,

    /// <summary>Rows grouped with optional subtotals (Phase 3 execution).</summary>
    Grouped,

    /// <summary>Aggregate-only summary rows (Phase 3 execution).</summary>
    Summary
}
