using System.Globalization;
using MetaForge.Shared.Culture;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Resolves effective user preferences by applying user overrides on top of system defaults.
/// </summary>
public class PreferenceResolverService : IPreferenceResolver
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly ISystemSettingsService _systemSettings;

    public PreferenceResolverService(MetaForgeDbContext dbContext, ISystemSettingsService systemSettings)
    {
        _dbContext = dbContext;
        _systemSettings = systemSettings;
    }

    public async Task<EffectivePreferencesDto> ResolveAsync(int? userId, CancellationToken cancellationToken = default)
    {
        var system = await _systemSettings.GetPreferencesAsync(cancellationToken);

        string? userCulture = null;
        string? userTheme = null;
        string? userDateFormat = null;
        string? userDateTimeFormat = null;

        if (userId is int id)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.CultureOverride,
                    u.ThemeKey,
                    u.DateFormatOverride,
                    u.DateTimeFormatOverride
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user != null)
            {
                userCulture = string.IsNullOrWhiteSpace(user.CultureOverride) ? null : user.CultureOverride;
                userTheme = string.IsNullOrWhiteSpace(user.ThemeKey) ? null : user.ThemeKey;
                userDateFormat = string.IsNullOrWhiteSpace(user.DateFormatOverride) ? null : user.DateFormatOverride;
                userDateTimeFormat = string.IsNullOrWhiteSpace(user.DateTimeFormatOverride) ? null : user.DateTimeFormatOverride;
            }
        }

        var effectiveCulture = userCulture ?? system.Localization.DefaultCulture;
        var effectiveTheme = AppThemes.Normalize(userTheme ?? system.Appearance.DefaultThemeKey);
        var systemDateFormat = DateFormatCatalog.NormalizeDateFormat(system.Localization.DefaultDateFormat, effectiveCulture);
        var systemDateTimeFormat = DateFormatCatalog.NormalizeDateTimeFormat(system.Localization.DefaultDateTimeFormat, effectiveCulture);

        CultureInfo cultureInfo;
        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(effectiveCulture);
        }
        catch (CultureNotFoundException)
        {
            cultureInfo = CultureInfo.GetCultureInfo(system.Localization.FallbackCulture);
            effectiveCulture = cultureInfo.Name;
        }

        return new EffectivePreferencesDto
        {
            Culture = effectiveCulture,
            ThemeKey = effectiveTheme,
            DateFormat = DateFormatCatalog.NormalizeDateFormat(userDateFormat ?? systemDateFormat, effectiveCulture),
            DateTimeFormat = DateFormatCatalog.NormalizeDateTimeFormat(userDateTimeFormat ?? systemDateTimeFormat, effectiveCulture),
            IsRtl = cultureInfo.TextInfo.IsRightToLeft,
            CultureIsUserOverride = userCulture != null,
            ThemeIsUserOverride = userTheme != null,
            DateFormatIsUserOverride = userDateFormat != null,
            DateTimeFormatIsUserOverride = userDateTimeFormat != null,
            System = system,
            User = new UserPreferenceOverridesDto
            {
                Culture = userCulture,
                ThemeKey = userTheme,
                DateFormat = userDateFormat,
                DateTimeFormat = userDateTimeFormat
            }
        };
    }
}
