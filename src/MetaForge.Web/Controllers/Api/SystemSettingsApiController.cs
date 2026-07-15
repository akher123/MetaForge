using System.Security.Claims;
using MetaForge.Shared.Culture;

namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/system/settings")]
public class SystemSettingsApiController : ControllerBase
{
    private readonly ISystemSettingsService _systemSettings;

    public SystemSettingsApiController(ISystemSettingsService systemSettings) => _systemSettings = systemSettings;

    [HttpGet]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _systemSettings.GetAllAsync(cancellationToken));

    [HttpGet("preferences")]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken) =>
        Ok(await _systemSettings.GetPreferencesAsync(cancellationToken));

    [HttpGet("localization")]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public async Task<IActionResult> GetLocalization(CancellationToken cancellationToken) =>
        Ok(await _systemSettings.GetLocalizationAsync(cancellationToken));

    [HttpGet("cultures")]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public IActionResult GetCultures() =>
        Ok(_systemSettings.GetAvailableCultures());

    [HttpGet("date-formats")]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public IActionResult GetDateFormats([FromQuery] string culture)
    {
        if (string.IsNullOrWhiteSpace(culture) || !CultureCatalog.TryNormalize(culture, out var normalized))
            return BadRequest(new { error = "A valid culture code is required." });

        return Ok(new
        {
            culture = normalized,
            dateFormats = DateFormatCatalog.GetDateOptions(normalized),
            dateTimeFormats = DateFormatCatalog.GetDateTimeOptions(normalized)
        });
    }

    [HttpPut("localization")]
    [RequirePermissionCode(SystemSettingsPermissions.Manage)]
    public async Task<IActionResult> UpdateLocalization(
        [FromBody] UpdateLocalizationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required." });

        await _systemSettings.UpdateLocalizationAsync(new LocalizationSettingsDto
        {
            Enabled = request.Enabled,
            DefaultCulture = request.DefaultCulture,
            FallbackCulture = request.FallbackCulture,
            DefaultDateFormat = request.DefaultDateFormat,
            DefaultDateTimeFormat = request.DefaultDateTimeFormat
        }, TryGetUserId(), cancellationToken);

        return Ok(await _systemSettings.GetLocalizationAsync(cancellationToken));
    }

    [HttpGet("appearance")]
    [RequirePermissionCode(SystemSettingsPermissions.View)]
    public async Task<IActionResult> GetAppearance(CancellationToken cancellationToken) =>
        Ok(await _systemSettings.GetAppearanceAsync(cancellationToken));

    [HttpPut("appearance")]
    [RequirePermissionCode(SystemSettingsPermissions.Manage)]
    public async Task<IActionResult> UpdateAppearance(
        [FromBody] UpdateAppearanceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.DefaultThemeKey))
            return BadRequest(new { error = "DefaultThemeKey is required." });

        await _systemSettings.UpdateAppearanceAsync(new AppearanceSettingsDto
        {
            DefaultThemeKey = request.DefaultThemeKey
        }, TryGetUserId(), cancellationToken);

        return Ok(await _systemSettings.GetAppearanceAsync(cancellationToken));
    }

    private int? TryGetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out var userId) ? userId : null;
    }
}
