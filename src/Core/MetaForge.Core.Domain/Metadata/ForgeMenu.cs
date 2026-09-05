namespace MetaForge.Domain.Metadata;

/// <summary>
/// Hierarchical navigation menu entry with optional form or URL link.
/// </summary>
public class ForgeMenu
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string ItemType { get; set; } = MenuItemType.Folder;

    public int? FormId { get; set; }

    public string? Action { get; set; }

    public string? Url { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ForgeMenu? Parent { get; set; }

    public ICollection<ForgeMenu> Children { get; set; } = [];

    public ForgeForm? Form { get; set; }
}
