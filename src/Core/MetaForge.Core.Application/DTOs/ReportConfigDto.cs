namespace MetaForge.Application.DTOs;

/// <summary>
/// Full report configuration for create/edit screens.
/// </summary>
public class ReportConfigDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string GroupName { get; set; } = "Reports";

    public string ReportType { get; set; } = "Tabular";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

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

    public List<ReportColumnConfigDto> Columns { get; set; } = [];

    public List<ReportFilterConfigDto> Filters { get; set; } = [];

    public List<ReportGroupConfigDto> Groups { get; set; } = [];

    public List<ReportSummaryConfigDto> Summaries { get; set; } = [];

    public List<ReportSignatureLineDto> Signatures { get; set; } = [];
}

public class ReportColumnConfigDto
{
    public int Id { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public string ColumnRole { get; set; } = "Detail";

    public string AggregateFunction { get; set; } = "None";

    public string? DisplayFormat { get; set; }

    public string? Formula { get; set; }
}

public class ReportFilterConfigDto
{
    public int Id { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Operator { get; set; } = "Equals";

    public string ControlType { get; set; } = "TextBox";

    public string? LookupEntity { get; set; }

    public string? Options { get; set; }

    public string? DefaultValue { get; set; }

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}

public class ReportGroupConfigDto
{
    public int Id { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool SortDescending { get; set; }

    public bool ShowSubtotal { get; set; } = true;

    public bool ShowGroupHeader { get; set; } = true;
}

public class ReportSummaryConfigDto
{
    public int Id { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string AggregateFunction { get; set; } = "Sum";

    public int DisplayOrder { get; set; }
}

public class ReportConfigListItemDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string ReportType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int ColumnCount { get; set; }

    public int FilterCount { get; set; }
}
