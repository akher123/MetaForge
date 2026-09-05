namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for running configured dynamic reports.
/// </summary>
[Authorize]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly IFormAuthorizationService _authorizationService;

    public ReportController(IReportService reportService, IFormAuthorizationService authorizationService)
    {
        _reportService = reportService;
        _authorizationService = authorizationService;
    }

    [HttpGet("/Reports/{reportCode}")]
    public async Task<IActionResult> Run(string reportCode, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, ReportPermissions.Run(reportCode), cancellationToken);
        if (denied != null) return denied;

        var definition = await _reportService.GetDefinitionAsync(reportCode, cancellationToken);
        if (definition == null) return NotFound();

        ViewBag.ReportCode = reportCode;
        ViewBag.Definition = definition;
        ViewBag.Permissions = new ReportPermissionsDto
        {
            ReportCode = reportCode,
            CanRun = true,
            CanExport = await _authorizationService.HasPermissionCodeAsync(
                User, ReportPermissions.Export(reportCode), cancellationToken)
        };

        return View("Run");
    }
}
