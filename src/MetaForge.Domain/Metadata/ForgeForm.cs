namespace MetaForge.Domain.Metadata;

/// <summary>
/// Configures a dynamic admin form mapped to a database entity.
/// </summary>
public class ForgeForm
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string? GroupName { get; set; }

    public FormType FormType { get; set; } = FormType.Master;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ForgeField> Fields { get; set; } = [];

    public ICollection<ForgeRelation> Relations { get; set; } = [];

    public ICollection<ForgeGridColumn> GridColumns { get; set; } = [];

    public ICollection<ForgeFormAction> GridActions { get; set; } = [];

    public ICollection<ForgeTreeLevel> TreeLevels { get; set; } = [];
}
