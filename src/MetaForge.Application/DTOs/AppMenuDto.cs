namespace MetaForge.Application.DTOs;

/// <summary>
/// Full application menu including system and module items.
/// </summary>
public class AppMenuDto
{
    public List<SystemMenuItemDto> SystemItems { get; set; } = [];

    public List<MenuGroupDto> FormGroups { get; set; } = [];
}

public class SystemMenuItemDto
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
