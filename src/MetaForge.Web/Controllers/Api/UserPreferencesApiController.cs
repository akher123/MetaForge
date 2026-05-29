using System.Security.Claims;
using MetaForge.Shared.Constants;
using MetaForge.Web.Theme;

namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/preferences")]
public class UserPreferencesApiController : ControllerBase
{
    private readonly IUserPreferenceService _preferences;

    public UserPreferencesApiController(IUserPreferenceService preferences) => _preferences = preferences;

    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var themeKey = await _preferences.GetThemeAsync(userId, cancellationToken);
        return Ok(BuildResponse(themeKey));
    }

    [HttpPut("theme")]
    public async Task<IActionResult> SetTheme([FromBody] SetThemeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request == null || string.IsNullOrWhiteSpace(request.ThemeKey))
            return BadRequest(new { error = "ThemeKey is required." });

        if (!AppThemes.IsValid(request.ThemeKey))
            return BadRequest(new { error = $"Unknown theme '{request.ThemeKey}'." });

        await _preferences.SetThemeAsync(userId, request.ThemeKey, cancellationToken);
        Response.Cookies.Append(
            ThemeCookie.Name,
            AppThemes.Normalize(request.ThemeKey),
            ThemeCookie.Options(HttpContext));

        return Ok(BuildResponse(AppThemes.Normalize(request.ThemeKey)));
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out userId);
    }

    private UserThemeResponse BuildResponse(string themeKey) =>
        new()
        {
            ThemeKey = themeKey,
            IsDark = AppThemes.IsDark(themeKey),
            Available = _preferences.GetAvailableThemes()
        };
}
