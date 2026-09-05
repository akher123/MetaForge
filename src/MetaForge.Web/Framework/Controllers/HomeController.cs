using MetaForge.Web.Models;

namespace MetaForge.Web.Controllers;

public class HomeController : Controller
{
    private readonly INavigationService _navigationService;
    private readonly IFormAuthorizationService _authorizationService;

    public HomeController(
        INavigationService navigationService,
        IFormAuthorizationService authorizationService)
    {
        _navigationService = navigationService;
        _authorizationService = authorizationService;
    }

    [AllowAnonymous]
    public IActionResult Landing() => View();

    [Authorize]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        ViewBag.Menu = await _navigationService.GetMenuAsync(cancellationToken);
        ViewBag.CanViewConfig = await _authorizationService.HasPermissionCodeAsync(User, ConfigPermissions.View, cancellationToken);
        ViewBag.CanManageConfig = await _authorizationService.HasPermissionCodeAsync(User, ConfigPermissions.Manage, cancellationToken);
        ViewBag.CanViewReportConfig = await _authorizationService.HasPermissionCodeAsync(User, ReportConfigPermissions.View, cancellationToken);
        ViewBag.CanViewSecurity = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewUsers, cancellationToken)
            || await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewRoles, cancellationToken)
            || await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewPermissions, cancellationToken);
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
