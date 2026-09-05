namespace MetaForge.Application.DTOs;

/// <summary>
/// Navigation menu item generated from modules.
/// </summary>
public class MenuGroupDto
{
    public string GroupName { get; set; } = string.Empty;

    public List<MenuItemDto> Items { get; set; } = [];
}

public class MenuItemDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
