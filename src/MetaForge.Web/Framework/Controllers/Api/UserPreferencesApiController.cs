using System.Security.Claims;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Culture;
using MetaForge.Web.Theme;

namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/preferences")]
public class UserPreferencesApiController : ControllerBase
{
    private readonly IUserPreferenceService _preferences;
    private readonly IPreferenceResolver _preferenceResolver;
    private readonly ISystemSettingsService _systemSettings;

    public UserPreferencesApiController(
        IUserPreferenceService preferences,
        IPreferenceResolver preferenceResolver,
        ISystemSettingsService systemSettings)
    {
        _preferences = preferences;
        _preferenceResolver = preferenceResolver;
        _systemSettings = systemSettings;
    }

    [HttpGet("cultures")]
    public IActionResult GetCultures() =>
        Ok(_systemSettings.GetAvailableCultures());

    [HttpGet("date-formats")]
    public async Task<IActionResult> GetDateFormats([FromQuery] string? culture, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);
        var cultureName = string.IsNullOrWhiteSpace(culture)
            ? effective.Culture
            : CultureCatalog.TryNormalize(culture, out var normalized) ? normalized : null;

        if (cultureName == null)
            return BadRequest(new { error = "A valid culture code is required." });

        return Ok(new
        {
            culture = cultureName,
            dateFormats = DateFormatCatalog.GetDateOptions(cultureName),
            dateTimeFormats = DateFormatCatalog.GetDateTimeOptions(cultureName),
            effectiveDateFormat = effective.DateFormat,
            effectiveDateTimeFormat = effective.DateTimeFormat,
            systemDateFormat = effective.System.Localization.DefaultDateFormat,
            systemDateTimeFormat = effective.System.Localization.DefaultDateTimeFormat
        });
    }

    [HttpPut("date-formats")]
    public async Task<IActionResult> SetDateFormats(
        [FromBody] SetDateFormatsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await _preferences.SetDateFormatsAsync(
            userId,
            request?.DateFormat,
            request?.DateTimeFormat,
            cancellationToken);

        return Ok(await _preferenceResolver.ResolveAsync(userId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetEffective(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return Ok(await _preferenceResolver.ResolveAsync(userId, cancellationToken));
    }

    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var themeKey = await _preferences.GetThemeAsync(userId, cancellationToken);
        return Ok(BuildThemeResponse(themeKey));
    }

    [HttpPut("theme")]
    public async Task<IActionResult> SetTheme([FromBody] SetThemeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request?.ThemeKey != null && !string.IsNullOrWhiteSpace(request.ThemeKey) && !AppThemes.IsValid(request.ThemeKey))
            return BadRequest(new { error = $"Unknown theme '{request.ThemeKey}'." });

        await _preferences.SetThemeAsync(userId, request?.ThemeKey, cancellationToken);
        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);

        Response.Cookies.Append(
            ThemeCookie.Name,
            effective.ThemeKey,
            ThemeCookie.Options(HttpContext));

        return Ok(BuildThemeResponse(effective.ThemeKey));
    }

    [HttpPut("culture")]
    public async Task<IActionResult> SetCulture([FromBody] SetCultureRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await _preferences.SetCultureAsync(userId, request?.Culture, cancellationToken);
        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);

        Response.Cookies.Append(
            CultureCookie.Name,
            effective.Culture,
            CultureCookie.Options(HttpContext));

        return Ok(effective);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetToSystemDefaults(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await _preferences.ResetToSystemDefaultsAsync(userId, cancellationToken);
        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);

        Response.Cookies.Append(
            ThemeCookie.Name,
            effective.ThemeKey,
            ThemeCookie.Options(HttpContext));

        Response.Cookies.Append(
            CultureCookie.Name,
            effective.Culture,
            CultureCookie.Options(HttpContext));

        return Ok(effective);
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out userId);
    }

    private UserThemeResponse BuildThemeResponse(string themeKey) =>
        new()
        {
            ThemeKey = themeKey,
            IsDark = AppThemes.IsDark(themeKey),
            Available = _preferences.GetAvailableThemes()
        };
}
