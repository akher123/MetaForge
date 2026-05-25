namespace MetaForge.Application.DTOs;

/// <summary>
/// Relationship metadata for master-detail screens.
/// </summary>
public class RelationDefinition
{
    public string RelationType { get; set; } = string.Empty;

    public string ParentEntity { get; set; } = string.Empty;

    public string ChildEntity { get; set; } = string.Empty;

    public string ForeignKey { get; set; } = string.Empty;

    public string? NavigationProperty { get; set; }

    public string? TabLabel { get; set; }

    public int DisplayOrder { get; set; }

    public FormDefinition? ChildForm { get; set; }
}
