namespace MetaForge.Domain.Enums;

/// <summary>
/// How a report column participates in layout and calculations.
/// </summary>
public enum ReportColumnRole
{
    /// <summary>Standard detail column shown in output.</summary>
    Detail,

    /// <summary>Field used to group rows.</summary>
    GroupBy,

    /// <summary>Numeric column with an aggregate function.</summary>
    Aggregate,

    /// <summary>Calculated expression column (Phase 4).</summary>
    Calculated
}
