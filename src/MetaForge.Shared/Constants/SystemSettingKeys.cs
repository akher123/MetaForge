namespace MetaForge.Shared.Constants;

/// <summary>
/// Stable keys for <see cref="MetaForge.Domain.Platform.SystemSetting"/> rows.
/// </summary>
public static class SystemSettingKeys
{
    public const string LocalizationEnabled = "localization.enabled";
    public const string DefaultCulture = "localization.defaultCulture";
    public const string FallbackCulture = "localization.fallbackCulture";
    public const string DefaultDateFormat = "localization.defaultDateFormat";
    public const string DefaultDateTimeFormat = "localization.defaultDateTimeFormat";

    public const string DefaultThemeKey = "appearance.defaultThemeKey";
}
