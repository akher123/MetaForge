using MetaForge.Application.DTOs;

namespace MetaForge.Web.Models;

public sealed record BreadcrumbItem(string Text, string? Url = null, bool IsCurrent = false)
{
    public bool IsLink => !string.IsNullOrWhiteSpace(Url);

    public static BreadcrumbItem Link(string text, string url) => new(text, url);

    public static BreadcrumbItem Label(string text) => new(text);

    public static BreadcrumbItem Current(string text) => new(text, IsCurrent: true);

    public static BreadcrumbItem FromDto(NavigationBreadcrumbDto dto) =>
        dto.IsCurrent ? Current(dto.Text)
        : !string.IsNullOrWhiteSpace(dto.Url) ? Link(dto.Text, dto.Url)
        : Label(dto.Text);
}
