using System.Security.Claims;
using MetaForge.Application.DTOs;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Culture;
using MetaForge.Shared.Exceptions;
using MetaForge.Web.Theme;
using MetaForge.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace MetaForge.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserClaimsFactory _claimsFactory;
    private readonly IUserPreferenceService _userPreferences;
    private readonly IPreferenceResolver _preferenceResolver;
    private readonly ISystemSettingsService _systemSettings;
    private readonly IPasswordResetService _passwordResetService;

    public AccountController(
        IAuthService authService,
        IUserClaimsFactory claimsFactory,
        IUserPreferenceService userPreferences,
        IPreferenceResolver preferenceResolver,
        ISystemSettingsService systemSettings,
        IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _claimsFactory = claimsFactory;
        _userPreferences = userPreferences;
        _preferenceResolver = preferenceResolver;
        _systemSettings = systemSettings;
        _passwordResetService = passwordResetService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        var model = new LoginViewModel { ReturnUrl = returnUrl };
        if (TempData["ResetSuccess"] is string resetSuccess)
            model.SuccessMessage = resetSuccess;

        return View(model);
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

        var effective = await _preferenceResolver.ResolveAsync(result.UserId, cancellationToken);
        Response.Cookies.Append(CultureCookie.Name, effective.Culture, CultureCookie.Options(HttpContext));

        return RedirectToLocal(model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard", "Home");

        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        await _passwordResetService.SendForgotPasswordAsync(model.EmailOrUserName, cancellationToken);

        model.SuccessMessage = "If an account matches that email or username, a password reset link has been sent.";
        model.EmailOrUserName = string.Empty;
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard", "Home");

        if (string.IsNullOrWhiteSpace(token))
            return View(new ResetPasswordViewModel { ErrorMessage = "Reset link is invalid or missing." });

        var info = await _passwordResetService.ValidateTokenAsync(token, cancellationToken);
        if (info == null)
            return View(new ResetPasswordViewModel { Token = token, ErrorMessage = "This reset link is invalid or has expired." });

        return View(new ResetPasswordViewModel
        {
            Token = token,
            UserName = info.UserName
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _passwordResetService.ResetPasswordAsync(model.Token, model.NewPassword, cancellationToken);
        }
        catch (BusinessException ex)
        {
            model.ErrorMessage = ex.Message;
            return View(model);
        }

        TempData["ResetSuccess"] = "Your password has been updated. You can sign in now.";
        return RedirectToAction(nameof(Login));
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
        var effective = await _preferenceResolver.ResolveAsync(userId, cancellationToken);

        return View(new AppearanceViewModel
        {
            ActiveThemeKey = themeKey,
            Themes = _userPreferences.GetAvailableThemes(),
            Culture = BuildCulturePickerViewModel(effective)
        });
    }

    private CulturePickerViewModel BuildCulturePickerViewModel(EffectivePreferencesDto effective) =>
        new()
        {
            EffectiveCulture = effective.Culture,
            UserCultureOverride = effective.User.Culture,
            SystemDefaultCulture = effective.System.Localization.DefaultCulture,
            CultureIsUserOverride = effective.CultureIsUserOverride,
            EffectiveDateFormat = effective.DateFormat,
            EffectiveDateTimeFormat = effective.DateTimeFormat,
            UserDateFormatOverride = effective.User.DateFormat,
            UserDateTimeFormatOverride = effective.User.DateTimeFormat,
            SystemDefaultDateFormat = effective.System.Localization.DefaultDateFormat,
            SystemDefaultDateTimeFormat = effective.System.Localization.DefaultDateTimeFormat,
            DateFormatIsUserOverride = effective.DateFormatIsUserOverride,
            DateTimeFormatIsUserOverride = effective.DateTimeFormatIsUserOverride,
            Preview = LocaleFormatting.BuildPreview(
                effective.Culture,
                effective.DateFormat,
                effective.DateTimeFormat),
            Cultures = _systemSettings.GetAvailableCultures()
        };

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
