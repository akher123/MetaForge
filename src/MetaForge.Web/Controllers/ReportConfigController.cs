namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for admin report metadata configuration.
/// </summary>
[Authorize]
public class ReportConfigController : Controller
{
    private readonly IReportConfigurationService _configService;

    public ReportConfigController(IReportConfigurationService configService) => _configService = configService;

    [HttpGet("/ReportBuilder")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ReportConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Reports = await _configService.GetAllReportsAsync(cancellationToken);
        return View();
    }

    [HttpGet("/ReportBuilder/Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ReportConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Entities = await _configService.GetDiscoveredEntitiesAsync(cancellationToken);
        ViewBag.IsEdit = false;
        return View("Form");
    }

    [HttpGet("/ReportBuilder/Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ReportConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        var report = await _configService.GetReportAsync(id, cancellationToken);
        if (report == null) return NotFound();

        ViewBag.Entities = await _configService.GetDiscoveredEntitiesAsync(cancellationToken);
        ViewBag.IsEdit = true;
        ViewBag.Report = report;
        return View("Form");
    }
}
