using System.Security.Claims;
using MetaForge.Shared.Constants;
using MetaForge.Web.Theme;
using MetaForge.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace MetaForge.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserClaimsFactory _claimsFactory;
    private readonly IUserPreferenceService _userPreferences;

    public AccountController(
        IAuthService authService,
        IUserClaimsFactory claimsFactory,
        IUserPreferenceService userPreferences)
    {
        _authService = authService;
        _claimsFactory = claimsFactory;
        _userPreferences = userPreferences;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _authService.AuthenticateAsync(model.UserName, model.Password, cancellationToken);
        if (result == null)
        {
            model.ErrorMessage = "Invalid username or password.";
            return View(model);
        }

        var principal = await _claimsFactory.CreatePrincipalAsync(result.UserId, cancellationToken);
        if (principal == null)
        {
            model.ErrorMessage = "Your account is inactive or unavailable.";
            return View(model);
        }

        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
        });

        var themeKey = await _userPreferences.GetThemeAsync(result.UserId, cancellationToken);
        Response.Cookies.Append(ThemeCookie.Name, themeKey, ThemeCookie.Options(HttpContext));

        return RedirectToLocal(model.ReturnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Landing", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Appearance(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Challenge();

        var themeKey = await _userPreferences.GetThemeAsync(userId, cancellationToken);
        return View(new AppearanceViewModel
        {
            ActiveThemeKey = themeKey,
            Themes = _userPreferences.GetAvailableThemes()
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Landing", "Home");
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out userId);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Dashboard", "Home");
    }
}
