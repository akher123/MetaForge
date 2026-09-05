namespace MetaForge.Application.Interfaces;

public interface ISystemSettingsService
{
    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SystemPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default);

    Task<LocalizationSettingsDto> GetLocalizationAsync(CancellationToken cancellationToken = default);

    Task<AppearanceSettingsDto> GetAppearanceAsync(CancellationToken cancellationToken = default);

    Task UpdateLocalizationAsync(LocalizationSettingsDto settings, int? updatedByUserId, CancellationToken cancellationToken = default);

    Task UpdateAppearanceAsync(AppearanceSettingsDto settings, int? updatedByUserId, CancellationToken cancellationToken = default);

    IReadOnlyList<CultureOptionDto> GetAvailableCultures();

    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    Task<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);
}

public interface IPreferenceResolver
{
    Task<EffectivePreferencesDto> ResolveAsync(int? userId, CancellationToken cancellationToken = default);
}
