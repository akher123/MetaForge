namespace MetaForge.Application.DTOs;

/// <summary>
/// Tree node for sidebar navigation rendering.
/// </summary>
public class MenuTreeNodeDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string ItemType { get; set; } = "Folder";

    public string? Url { get; set; }

    public List<MenuTreeNodeDto> Children { get; set; } = [];
}

/// <summary>
/// Menu entry for create/edit administration screens.
/// </summary>
public class MenuEntryDto
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string ItemType { get; set; } = "Folder";

    public int? FormId { get; set; }

    public string? Action { get; set; }

    public string? Url { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class MenuListItemDto
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string? FormName { get; set; }

    public string? Url { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public int Depth { get; set; }
}

public class MenuParentOptionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Depth { get; set; }
}

public class MenuFormOptionDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
