namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/reports")]
public class ReportApiController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IFormAuthorizationService _authorizationService;

    public ReportApiController(IReportService reportService, IFormAuthorizationService authorizationService)
    {
        _reportService = reportService;
        _authorizationService = authorizationService;
    }

    [HttpGet("{reportCode}")]
    public async Task<IActionResult> GetDefinition(string reportCode, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, ReportPermissions.Run(reportCode), cancellationToken);
        if (denied != null) return denied;

        var definition = await _reportService.GetDefinitionAsync(reportCode, cancellationToken);
        if (definition == null) return NotFound();

        var permissions = await BuildPermissionsAsync(reportCode, cancellationToken);
        return Ok(new { definition, permissions });
    }

    [HttpPost("{reportCode}/data")]
    public async Task<IActionResult> GetData(
        string reportCode,
        [FromBody] ReportQueryRequest? request,
        CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, ReportPermissions.Run(reportCode), cancellationToken);
        if (denied != null) return denied;

        request ??= new ReportQueryRequest();
        return Ok(await _reportService.ExecuteAsync(reportCode, request, cancellationToken));
    }

    [HttpGet("{reportCode}/export/excel")]
    public async Task<IActionResult> ExportExcel(
        string reportCode,
        [FromQuery] ReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, ReportPermissions.Export(reportCode), cancellationToken);
        if (denied != null) return denied;

        var bytes = await _reportService.ExportExcelAsync(reportCode, request, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{reportCode}.xlsx");
    }

    [HttpGet("{reportCode}/export/pdf")]
    public async Task<IActionResult> ExportPdf(
        string reportCode,
        [FromQuery] ReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(
            HttpContext, ReportPermissions.Export(reportCode), cancellationToken);
        if (denied != null) return denied;

        var bytes = await _reportService.ExportPdfAsync(reportCode, request, cancellationToken);
        return File(bytes, "application/pdf", $"{reportCode}.pdf");
    }

    private async Task<ReportPermissionsDto> BuildPermissionsAsync(string reportCode, CancellationToken cancellationToken)
    {
        var canRun = await _authorizationService.HasPermissionCodeAsync(User, ReportPermissions.Run(reportCode), cancellationToken);
        var canExport = await _authorizationService.HasPermissionCodeAsync(User, ReportPermissions.Export(reportCode), cancellationToken);
        return new ReportPermissionsDto
        {
            ReportCode = reportCode,
            CanRun = canRun,
            CanExport = canExport
        };
    }
}
