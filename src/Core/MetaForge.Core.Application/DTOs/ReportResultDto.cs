namespace MetaForge.Application.DTOs;

/// <summary>
/// Unified runtime result for tabular, grouped, and summary reports.
/// </summary>
public class ReportResultDto
{
    public string ReportType { get; set; } = "Tabular";

    public List<ReportColumnDefinitionDto> Columns { get; set; } = [];

    public List<ReportRowDto> Rows { get; set; } = [];

    public Dictionary<string, object?> GrandTotals { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int DetailCount { get; set; }
}

public class ReportRowDto
{
    public string RowType { get; set; } = ReportRowTypes.Detail;

    public int Level { get; set; }

    public string? Label { get; set; }

    public Dictionary<string, object?> Values { get; set; } = [];
}

public static class ReportRowTypes
{
    public const string Detail = "Detail";
    public const string GroupHeader = "GroupHeader";
    public const string GroupSubtotal = "GroupSubtotal";
    public const string GrandTotal = "GrandTotal";
    public const string Summary = "Summary";
}
