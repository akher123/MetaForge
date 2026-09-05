namespace MetaForge.Domain.Metadata;

/// <summary>
/// Column configuration for dynamic data grids.
/// </summary>
public class ForgeGridColumn
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsSortable { get; set; } = true;

    public bool IsSearchable { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Optional display format preset key or custom .NET format string for date/date-time columns.
    /// </summary>
    public string? DisplayFormat { get; set; }

    public ForgeForm Form { get; set; } = null!;
}
