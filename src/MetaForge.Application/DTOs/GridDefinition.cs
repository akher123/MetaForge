namespace MetaForge.Application.DTOs;

/// <summary>
/// Grid configuration for dynamic data tables.
/// </summary>
public class GridDefinition
{
    public string Entity { get; set; } = string.Empty;

    public string FormCode { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public List<GridColumnDefinition> Columns { get; set; } = [];
}

public class GridColumnDefinition
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsSortable { get; set; } = true;

    public bool IsSearchable { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public string? ControlType { get; set; }

    public string? LookupEntity { get; set; }

    public string? LookupParentField { get; set; }

    public string? LookupFilterField { get; set; }
}
