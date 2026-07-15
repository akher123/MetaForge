using MetaForge.Shared.Constants;

namespace MetaForge.Application.DTOs;

public sealed class SystemSettingDto
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public required string ValueType { get; init; }

    public string? Description { get; init; }

    public required string Category { get; init; }

    public bool IsEditable { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class LocalizationSettingsDto
{
    public bool Enabled { get; set; } = true;

    public string DefaultCulture { get; set; } = "en-US";

    public string FallbackCulture { get; set; } = "en-US";

    public string DefaultDateFormat { get; set; } = GridDisplayFormats.LocaleDate;

    public string DefaultDateTimeFormat { get; set; } = GridDisplayFormats.LocaleDateTime;
}

public sealed class CultureOptionDto
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string EnglishName { get; init; }

    public required string NativeName { get; init; }

    public bool IsRtl { get; init; }
}

public sealed class AppearanceSettingsDto
{
    public string DefaultThemeKey { get; set; } = AppThemes.Default;
}

public sealed class SystemPreferencesDto
{
    public required LocalizationSettingsDto Localization { get; init; }

    public required AppearanceSettingsDto Appearance { get; init; }
}

public sealed class UpdateLocalizationSettingsRequest
{
    public bool Enabled { get; set; } = true;

    public string DefaultCulture { get; set; } = "en-US";

    public string FallbackCulture { get; set; } = "en-US";

    public string DefaultDateFormat { get; set; } = GridDisplayFormats.LocaleDate;

    public string DefaultDateTimeFormat { get; set; } = GridDisplayFormats.LocaleDateTime;
}

public sealed class UpdateAppearanceSettingsRequest
{
    public string DefaultThemeKey { get; set; } = AppThemes.Default;
}

public sealed class EffectivePreferencesDto
{
    public required string Culture { get; init; }

    public required string ThemeKey { get; init; }

    public required string DateFormat { get; init; }

    public required string DateTimeFormat { get; init; }

    public bool IsRtl { get; init; }

    public bool CultureIsUserOverride { get; init; }

    public bool ThemeIsUserOverride { get; init; }

    public bool DateFormatIsUserOverride { get; init; }

    public bool DateTimeFormatIsUserOverride { get; init; }

    public required SystemPreferencesDto System { get; init; }

    public UserPreferenceOverridesDto User { get; init; } = new();
}

public sealed class UserPreferenceOverridesDto
{
    public string? Culture { get; init; }

    public string? ThemeKey { get; init; }

    public string? DateFormat { get; init; }

    public string? DateTimeFormat { get; init; }
}
