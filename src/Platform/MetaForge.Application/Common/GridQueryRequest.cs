namespace MetaForge.Application.Common;

/// <summary>
/// Grid query parameters.
/// </summary>
public class GridQueryRequest
{
    public string Entity { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortColumn { get; set; }

    public bool SortDescending { get; set; }

    public string? SearchTerm { get; set; }

    public Dictionary<string, string>? Filters { get; set; }
}
