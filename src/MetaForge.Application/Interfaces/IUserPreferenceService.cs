namespace MetaForge.Application.Interfaces;

/// <summary>
/// Per-user UI preferences (theme, etc.).
/// </summary>
public interface IUserPreferenceService
{
    Task<string> GetThemeAsync(int userId, CancellationToken cancellationToken = default);

    Task SetThemeAsync(int userId, string themeKey, CancellationToken cancellationToken = default);

    IReadOnlyList<ThemeOptionDto> GetAvailableThemes();
}

public sealed class ThemeOptionDto
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public bool IsDark { get; init; }

    public required string PrimarySwatch { get; init; }

    public required string AccentSwatch { get; init; }

    public required string BackgroundSwatch { get; init; }

    public required string SurfaceSwatch { get; init; }

    public required string Description { get; init; }
}

public sealed class SetThemeRequest
{
    public string ThemeKey { get; set; } = string.Empty;
}

public sealed class UserThemeResponse
{
    public required string ThemeKey { get; init; }

    public bool IsDark { get; init; }

    public IReadOnlyList<ThemeOptionDto> Available { get; init; } = [];
}
