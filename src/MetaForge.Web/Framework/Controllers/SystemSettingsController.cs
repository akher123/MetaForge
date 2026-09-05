namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for platform system settings.
/// </summary>
[Authorize]
public class SystemSettingsController : Controller
{
    private readonly ISystemSettingsService _systemSettings;
    private readonly IUserPreferenceService _userPreferences;

    public SystemSettingsController(
        ISystemSettingsService systemSettings,
        IUserPreferenceService userPreferences)
    {
        _systemSettings = systemSettings;
        _userPreferences = userPreferences;
    }

    [HttpGet("/SystemSettings")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, SystemSettingsPermissions.View, cancellationToken);
        if (denied != null)
            return denied;

        ViewBag.Preferences = await _systemSettings.GetPreferencesAsync(cancellationToken);
        ViewBag.Themes = _userPreferences.GetAvailableThemes();
        ViewBag.CanManage = await CanManageAsync(cancellationToken);
        return View();
    }

    private async Task<bool> CanManageAsync(CancellationToken cancellationToken)
    {
        var authService = HttpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        return await authService.HasPermissionCodeAsync(HttpContext.User, SystemSettingsPermissions.Manage, cancellationToken);
    }
}
