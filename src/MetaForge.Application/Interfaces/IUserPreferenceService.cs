namespace MetaForge.Application.Interfaces;

/// <summary>
/// Per-user UI preferences (theme, culture, etc.).
/// </summary>
public interface IUserPreferenceService
{
    Task<string> GetThemeAsync(int userId, CancellationToken cancellationToken = default);

    Task SetThemeAsync(int userId, string? themeKey, CancellationToken cancellationToken = default);

    Task<string?> GetCultureOverrideAsync(int userId, CancellationToken cancellationToken = default);

    Task SetCultureAsync(int userId, string? culture, CancellationToken cancellationToken = default);

    Task SetDateFormatsAsync(
        int userId,
        string? dateFormat,
        string? dateTimeFormat,
        CancellationToken cancellationToken = default);

    Task ResetToSystemDefaultsAsync(int userId, CancellationToken cancellationToken = default);

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
    /// <summary>Theme key, or null/empty to inherit the system default.</summary>
    public string? ThemeKey { get; set; }
}

public sealed class SetCultureRequest
{
    /// <summary>Culture code (e.g. en-US), or null/empty to inherit the system default.</summary>
    public string? Culture { get; set; }
}

public sealed class SetDateFormatsRequest
{
    /// <summary>Date format key, or null/empty to inherit the system default.</summary>
    public string? DateFormat { get; set; }

    /// <summary>Date-time format key, or null/empty to inherit the system default.</summary>
    public string? DateTimeFormat { get; set; }
}

public sealed class UserThemeResponse
{
    public required string ThemeKey { get; init; }

    public bool IsDark { get; init; }

    public IReadOnlyList<ThemeOptionDto> Available { get; init; } = [];
}
