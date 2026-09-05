namespace MetaForge.Domain.Metadata;

/// <summary>
/// Relationship configuration between entities for master-detail screens.
/// </summary>
public class ForgeRelation
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public string RelationType { get; set; } = string.Empty;

    public string ParentEntity { get; set; } = string.Empty;

    public string ChildEntity { get; set; } = string.Empty;

    public string ForeignKey { get; set; } = string.Empty;

    public string? NavigationProperty { get; set; }

    public string? TabLabel { get; set; }

    public int DisplayOrder { get; set; }

    public ForgeForm Form { get; set; } = null!;
}
