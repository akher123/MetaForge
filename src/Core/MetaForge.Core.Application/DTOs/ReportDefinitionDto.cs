namespace MetaForge.Application.DTOs;

/// <summary>
/// Runtime report definition for the report viewer.
/// </summary>
public class ReportDefinitionDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string ReportType { get; set; } = "Tabular";

    public string? Description { get; set; }

    public List<ReportColumnDefinitionDto> Columns { get; set; } = [];

    public List<ReportFilterDefinitionDto> Filters { get; set; } = [];

    public List<ReportGroupDefinitionDto> Groups { get; set; } = [];

    public List<ReportSummaryDefinitionDto> Summaries { get; set; } = [];

    public ReportExportLayoutDto ExportLayout { get; set; } = new();
}

public class ReportGroupDefinitionDto
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool SortDescending { get; set; }

    public bool ShowSubtotal { get; set; } = true;

    public bool ShowGroupHeader { get; set; } = true;
}

public class ReportSummaryDefinitionDto
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string AggregateFunction { get; set; } = "Sum";
}

public class ReportColumnDefinitionDto
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsSortable { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public string ColumnRole { get; set; } = "Detail";

    public string AggregateFunction { get; set; } = "None";

    public string? ControlType { get; set; }

    public string? LookupEntity { get; set; }

    public string? DisplayFormat { get; set; }

    public string? Formula { get; set; }
}

public class ReportFilterDefinitionDto
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Operator { get; set; } = "Equals";

    public string ControlType { get; set; } = "TextBox";

    public string? LookupEntity { get; set; }

    /// <summary>Comma-separated static dropdown values.</summary>
    public string? Options { get; set; }

    public string? DefaultValue { get; set; }

    public bool IsRequired { get; set; }
}

public class ReportPermissionsDto
{
    public string ReportCode { get; set; } = string.Empty;

    public bool CanRun { get; set; }

    public bool CanExport { get; set; }
}
