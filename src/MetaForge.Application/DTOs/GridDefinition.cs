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

    public List<GridActionDefinition> Actions { get; set; } = [];
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

public class GridActionDefinition
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string Placement { get; set; } = "Row";

    public string HandlerType { get; set; } = "Api";

    public string HandlerTarget { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";

    public string? RequestBody { get; set; }

    public string? PermissionAction { get; set; }

    public string? ConfirmMessage { get; set; }

    public string ButtonStyle { get; set; } = "outline-primary";
}
