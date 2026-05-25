namespace MetaForge.Application.DTOs;

/// <summary>
/// Discovered entity metadata from EF Core model.
/// </summary>
public class EntityMetadataDto
{
    public string EntityName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string? PrimaryKey { get; set; }

    public List<EntityPropertyMetadataDto> Properties { get; set; } = [];

    public List<EntityRelationMetadataDto> Relations { get; set; } = [];
}

public class EntityPropertyMetadataDto
{
    public string Name { get; set; } = string.Empty;

    public string ClrType { get; set; } = string.Empty;

    public bool IsKey { get; set; }

    public bool IsForeignKey { get; set; }

    public int? MaxLength { get; set; }

    public bool IsNullable { get; set; }
}

public class EntityRelationMetadataDto
{
    public string RelationType { get; set; } = string.Empty;

    public string ParentEntity { get; set; } = string.Empty;

    public string ChildEntity { get; set; } = string.Empty;

    public string ForeignKey { get; set; } = string.Empty;

    public string? NavigationProperty { get; set; }
}
