namespace MetaForge.Application.Configuration;

/// <summary>
/// Export limits and defaults for grid and report downloads.
/// </summary>
public class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>Maximum rows included in a single Excel/CSV export.</summary>
    public int MaxExportRows { get; set; } = 10_000;
}
