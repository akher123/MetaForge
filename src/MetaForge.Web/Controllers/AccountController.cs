using System.Security.Claims;
using MetaForge.Web.Models;
using Microsoft.AspNetCore.Authentication;

namespace MetaForge.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserClaimsFactory _claimsFactory;

    public AccountController(IAuthService authService, IUserClaimsFactory claimsFactory)
    {
        _authService = authService;
        _claimsFactory = claimsFactory;
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
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Landing", "Home");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Dashboard", "Home");
    }
}
