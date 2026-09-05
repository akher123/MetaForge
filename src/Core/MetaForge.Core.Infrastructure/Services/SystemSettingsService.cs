using System.Globalization;
using MetaForge.Domain.Platform;
using MetaForge.Shared.Culture;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Loads and persists platform-wide system settings from the database.
/// </summary>
public class SystemSettingsService : ISystemSettingsService
{
    private const string SnapshotCacheKey = "system-settings:snapshot";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly MetaForgeDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public SystemSettingsService(MetaForgeDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.Values
            .OrderBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .Select(Map)
            .ToList();
    }

    public async Task<SystemPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var localization = await GetLocalizationAsync(cancellationToken);
        var appearance = await GetAppearanceAsync(cancellationToken);
        return new SystemPreferencesDto
        {
            Localization = localization,
            Appearance = appearance
        };
    }

    public async Task<LocalizationSettingsDto> GetLocalizationAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return new LocalizationSettingsDto
        {
            Enabled = GetBool(snapshot, SystemSettingKeys.LocalizationEnabled, true),
            DefaultCulture = GetString(snapshot, SystemSettingKeys.DefaultCulture, "en-US"),
            FallbackCulture = GetString(snapshot, SystemSettingKeys.FallbackCulture, "en-US"),
            DefaultDateFormat = GetString(snapshot, SystemSettingKeys.DefaultDateFormat, GridDisplayFormats.LocaleDate),
            DefaultDateTimeFormat = GetString(snapshot, SystemSettingKeys.DefaultDateTimeFormat, GridDisplayFormats.LocaleDateTime)
        };
    }

    public async Task<AppearanceSettingsDto> GetAppearanceAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return new AppearanceSettingsDto
        {
            DefaultThemeKey = AppThemes.Normalize(GetString(snapshot, SystemSettingKeys.DefaultThemeKey, AppThemes.Default))
        };
    }

    public IReadOnlyList<CultureOptionDto> GetAvailableCultures() =>
        CultureCatalog.GetSpecificCultures()
            .Select(c => new CultureOptionDto
            {
                Name = c.Name,
                DisplayName = c.DisplayName,
                EnglishName = c.EnglishName,
                NativeName = c.NativeName,
                IsRtl = c.TextInfo.IsRightToLeft
            })
            .ToList();

    public async Task UpdateLocalizationAsync(
        LocalizationSettingsDto settings,
        int? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateLocalization(settings);

        var defaultCulture = NormalizeCulture(settings.DefaultCulture);
        var fallbackCulture = NormalizeCulture(settings.FallbackCulture);
        var defaultDateFormat = DateFormatCatalog.NormalizeDateFormat(settings.DefaultDateFormat, defaultCulture);
        var defaultDateTimeFormat = DateFormatCatalog.NormalizeDateTimeFormat(settings.DefaultDateTimeFormat, defaultCulture);

        await UpsertAsync(SystemSettingKeys.LocalizationEnabled, settings.Enabled.ToString(), SystemSettingValueTypes.Bool,
            SystemSettingCategories.Localization, "Enable localization for the application.", updatedByUserId, cancellationToken);
        await UpsertAsync(SystemSettingKeys.DefaultCulture, defaultCulture, SystemSettingValueTypes.String,
            SystemSettingCategories.Localization, "Default culture for users without an override.", updatedByUserId, cancellationToken);
        await UpsertAsync(SystemSettingKeys.FallbackCulture, fallbackCulture, SystemSettingValueTypes.String,
            SystemSettingCategories.Localization, "Fallback culture when a translation is missing.", updatedByUserId, cancellationToken);
        await UpsertAsync(SystemSettingKeys.DefaultDateFormat, defaultDateFormat, SystemSettingValueTypes.String,
            SystemSettingCategories.Localization, "Default date display format for the default culture.", updatedByUserId, cancellationToken);
        await UpsertAsync(SystemSettingKeys.DefaultDateTimeFormat, defaultDateTimeFormat, SystemSettingValueTypes.String,
            SystemSettingCategories.Localization, "Default date-time display format for the default culture.", updatedByUserId, cancellationToken);

        InvalidateCache();
    }

    public async Task UpdateAppearanceAsync(
        AppearanceSettingsDto settings,
        int? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!AppThemes.IsValid(settings.DefaultThemeKey))
            throw new BusinessException($"Unknown theme '{settings.DefaultThemeKey}'.");

        var themeKey = AppThemes.Normalize(settings.DefaultThemeKey);
        await UpsertAsync(SystemSettingKeys.DefaultThemeKey, themeKey, SystemSettingValueTypes.String,
            SystemSettingCategories.Appearance, "Default UI theme for users without an override.", updatedByUserId, cancellationToken);

        InvalidateCache();
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.TryGetValue(key, out var setting) ? setting.Value : null;
    }

    public async Task<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        if (!snapshot.TryGetValue(key, out var setting))
            return defaultValue;

        return ParseValue(setting, defaultValue);
    }

    private async Task<IReadOnlyDictionary<string, SystemSetting>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(SnapshotCacheKey, out IReadOnlyDictionary<string, SystemSetting>? cached) && cached != null)
            return cached;

        var rows = await _dbContext.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);
        var snapshot = rows.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _cache.Set(SnapshotCacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private async Task UpsertAsync(
        string key,
        string value,
        string valueType,
        string category,
        string description,
        int? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing == null)
        {
            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                ValueType = valueType,
                Category = category,
                Description = description,
                IsEditable = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = updatedByUserId
            });
        }
        else
        {
            existing.Value = value;
            existing.ValueType = valueType;
            existing.Category = category;
            existing.Description = description;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SystemSettingDto Map(SystemSetting setting) =>
        new()
        {
            Key = setting.Key,
            Value = setting.Value,
            ValueType = setting.ValueType,
            Description = setting.Description,
            Category = setting.Category,
            IsEditable = setting.IsEditable,
            UpdatedAtUtc = setting.UpdatedAtUtc
        };

    private static string GetString(IReadOnlyDictionary<string, SystemSetting> snapshot, string key, string fallback) =>
        snapshot.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value)
            ? setting.Value
            : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, SystemSetting> snapshot, string key, bool fallback) =>
        snapshot.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var parsed)
            ? parsed
            : fallback;

    private static T ParseValue<T>(SystemSetting setting, T defaultValue) =>
        defaultValue switch
        {
            bool when bool.TryParse(setting.Value, out var boolValue) => (T)(object)boolValue,
            int when int.TryParse(setting.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) => (T)(object)intValue,
            string => (T)(object)setting.Value,
            _ => defaultValue
        };

    private static void ValidateLocalization(LocalizationSettingsDto settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DefaultCulture))
            throw new BusinessException("Default culture is required.");

        if (string.IsNullOrWhiteSpace(settings.FallbackCulture))
            throw new BusinessException("Fallback culture is required.");

        NormalizeCulture(settings.DefaultCulture);
        NormalizeCulture(settings.FallbackCulture);

        if (!DateFormatCatalog.IsValidDateFormat(settings.DefaultDateFormat, settings.DefaultCulture))
            throw new BusinessException($"Date format '{settings.DefaultDateFormat}' is not valid for culture '{settings.DefaultCulture}'.");

        if (!DateFormatCatalog.IsValidDateTimeFormat(settings.DefaultDateTimeFormat, settings.DefaultCulture))
            throw new BusinessException($"Date-time format '{settings.DefaultDateTimeFormat}' is not valid for culture '{settings.DefaultCulture}'.");
    }

    private static string NormalizeCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            throw new BusinessException("Culture code cannot be empty.");

        try
        {
            return CultureCatalog.NormalizeOrThrow(culture);
        }
        catch (CultureNotFoundException)
        {
            throw new BusinessException($"Culture '{culture}' is not supported by the .NET runtime.");
        }
    }

    private void InvalidateCache() => _cache.Remove(SnapshotCacheKey);
}
