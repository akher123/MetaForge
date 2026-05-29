namespace MetaForge.Web.Models;

public sealed class AppearanceViewModel
{
    public required string ActiveThemeKey { get; init; }

    public IReadOnlyList<MetaForge.Application.Interfaces.ThemeOptionDto> Themes { get; init; } = [];
}
