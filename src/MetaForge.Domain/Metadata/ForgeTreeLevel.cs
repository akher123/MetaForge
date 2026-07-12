namespace MetaForge.Domain.Metadata;

/// <summary>
/// One level in a multi-table tree screen (ordered entity chain).
/// </summary>
public class ForgeTreeLevel
{
    public int Id { get; set; }

    public int FormId { get; set; }

    /// <summary>0 = root level entity.</summary>
    public int LevelIndex { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? ParentEntity { get; set; }

    public string? ForeignKey { get; set; }

    public string DisplayColumn { get; set; } = "Name";

    public int DisplayOrder { get; set; }

    public ForgeForm Form { get; set; } = null!;
}
