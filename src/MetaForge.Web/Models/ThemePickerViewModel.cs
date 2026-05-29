using MetaForge.Application.Interfaces;

namespace MetaForge.Web.Models;

public sealed class ThemePickerViewModel
{
    public required string ActiveThemeKey { get; init; }

    public IReadOnlyList<ThemeOptionDto> Themes { get; init; } = [];

    /// <summary>compact | panel | page</summary>
    public string Variant { get; init; } = "panel";

    public string GridId { get; init; } = "themePickerGrid";
}
