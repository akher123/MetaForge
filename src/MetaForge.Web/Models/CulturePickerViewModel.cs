using MetaForge.Application.DTOs;
using MetaForge.Shared.Culture;

namespace MetaForge.Web.Models;

public sealed class CulturePickerViewModel
{
    /// <summary>Effective culture for the current user (override or system default).</summary>
    public required string EffectiveCulture { get; init; }

    /// <summary>User override, or null when inheriting the system default.</summary>
    public string? UserCultureOverride { get; init; }

    public required string SystemDefaultCulture { get; init; }

    public bool CultureIsUserOverride { get; init; }

    public required string EffectiveDateFormat { get; init; }

    public required string EffectiveDateTimeFormat { get; init; }

    public string? UserDateFormatOverride { get; init; }

    public string? UserDateTimeFormatOverride { get; init; }

    public required string SystemDefaultDateFormat { get; init; }

    public required string SystemDefaultDateTimeFormat { get; init; }

    public bool DateFormatIsUserOverride { get; init; }

    public bool DateTimeFormatIsUserOverride { get; init; }

    public required CulturePreviewDto Preview { get; init; }

    public IReadOnlyList<CultureOptionDto> Cultures { get; init; } = [];
}
