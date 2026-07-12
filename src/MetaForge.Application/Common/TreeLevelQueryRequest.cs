namespace MetaForge.Application.Common;

/// <summary>
/// Query parameters for a single level in a multi-table tree grid.
/// </summary>
public class TreeLevelQueryRequest
{
    public string FormCode { get; set; } = string.Empty;

    public int LevelIndex { get; set; }

    public int? ParentId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortColumn { get; set; }

    public bool SortDescending { get; set; }

    public string? SearchTerm { get; set; }
}
