using System.Globalization;
using System.Security.Claims;
using MetaForge.Application.Interfaces;
using MetaForge.Shared.Culture;
using MetaForge.Web.Theme;

namespace MetaForge.Web.Middleware;

/// <summary>
/// Resolves the effective culture per request and sets <see cref="CultureInfo.CurrentCulture"/>.
/// </summary>
public sealed class CultureMiddleware
{
    public const string EffectiveCultureItemKey = "MetaForge.EffectiveCulture";
    public const string IsRtlItemKey = "MetaForge.IsRtl";
    public const string EffectivePreferencesItemKey = "MetaForge.EffectivePreferences";

    private readonly RequestDelegate _next;

    public CultureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IPreferenceResolver preferenceResolver,
        ISystemSettingsService systemSettings)
    {
        var (cultureName, isRtl, dateFormat, dateTimeFormat) =
            await ResolveCultureAsync(context, preferenceResolver, systemSettings);

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        DisplayFormatContext.Preferences = new DisplayFormatPreferences
        {
            DateFormat = dateFormat,
            DateTimeFormat = dateTimeFormat
        };

        context.Items[EffectiveCultureItemKey] = culture.Name;
        context.Items[IsRtlItemKey] = isRtl;

        await _next(context);
    }

    private static async Task<(string Culture, bool IsRtl, string DateFormat, string DateTimeFormat)> ResolveCultureAsync(
        HttpContext context,
        IPreferenceResolver preferenceResolver,
        ISystemSettingsService systemSettings)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var effective = await preferenceResolver.ResolveAsync(userId, context.RequestAborted);
            context.Items[EffectivePreferencesItemKey] = effective;
            return (effective.Culture, effective.IsRtl, effective.DateFormat, effective.DateTimeFormat);
        }

        if (context.Request.Cookies.TryGetValue(CultureCookie.Name, out var cookieCulture)
            && CultureCatalog.TryNormalize(cookieCulture, out var normalized))
        {
            var cultureInfo = CultureInfo.GetCultureInfo(normalized);
            var localization = await systemSettings.GetLocalizationAsync(context.RequestAborted);
            return (
                cultureInfo.Name,
                cultureInfo.TextInfo.IsRightToLeft,
                DateFormatCatalog.NormalizeDateFormat(localization.DefaultDateFormat, cultureInfo.Name),
                DateFormatCatalog.NormalizeDateTimeFormat(localization.DefaultDateTimeFormat, cultureInfo.Name));
        }

        var localizationSettings = await systemSettings.GetLocalizationAsync(context.RequestAborted);
        try
        {
            var systemCulture = CultureInfo.GetCultureInfo(localizationSettings.DefaultCulture);
            return (
                systemCulture.Name,
                systemCulture.TextInfo.IsRightToLeft,
                DateFormatCatalog.NormalizeDateFormat(localizationSettings.DefaultDateFormat, systemCulture.Name),
                DateFormatCatalog.NormalizeDateTimeFormat(localizationSettings.DefaultDateTimeFormat, systemCulture.Name));
        }
        catch (CultureNotFoundException)
        {
            var fallback = CultureInfo.GetCultureInfo(localizationSettings.FallbackCulture);
            return (
                fallback.Name,
                fallback.TextInfo.IsRightToLeft,
                DateFormatCatalog.NormalizeDateFormat(localizationSettings.DefaultDateFormat, fallback.Name),
                DateFormatCatalog.NormalizeDateTimeFormat(localizationSettings.DefaultDateTimeFormat, fallback.Name));
        }
    }
}
