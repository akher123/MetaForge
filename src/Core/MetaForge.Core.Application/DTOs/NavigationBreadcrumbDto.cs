namespace MetaForge.Application.DTOs;

public class NavigationBreadcrumbDto
{
    public string Text { get; set; } = string.Empty;

    public string? Url { get; set; }

    public bool IsCurrent { get; set; }
}
