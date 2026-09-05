namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/reportconfig")]
public class ReportConfigApiController : ControllerBase
{
    private readonly IReportConfigurationService _configService;

    public ReportConfigApiController(IReportConfigurationService configService) => _configService = configService;

    [HttpGet]
    [RequirePermissionCode(ReportConfigPermissions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _configService.GetAllReportsAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [RequirePermissionCode(ReportConfigPermissions.View)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var report = await _configService.GetReportAsync(id, cancellationToken);
        return report == null ? NotFound() : Ok(report);
    }

    [HttpGet("discovered")]
    [RequirePermissionCode(ReportConfigPermissions.View)]
    public async Task<IActionResult> GetDiscovered(CancellationToken cancellationToken) =>
        Ok(await _configService.GetDiscoveredEntitiesAsync(cancellationToken));

    [HttpGet("properties/{entityName}")]
    [RequirePermissionCode(ReportConfigPermissions.View)]
    public async Task<IActionResult> GetPropertyPaths(string entityName, CancellationToken cancellationToken) =>
        Ok(await _configService.GetEntityPropertyPathsAsync(entityName, cancellationToken));

    [HttpGet("draft/{entityName}")]
    [RequirePermissionCode(ReportConfigPermissions.Manage)]
    public async Task<IActionResult> BuildDraft(
        string entityName,
        [FromQuery] string groupName = "Reports",
        CancellationToken cancellationToken = default) =>
        Ok(await _configService.BuildDraftAsync(entityName, groupName, cancellationToken));

    [HttpPost]
    [RequirePermissionCode(ReportConfigPermissions.Manage)]
    public async Task<IActionResult> Save([FromBody] ReportConfigDto config, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveReportAsync(config, cancellationToken);
        return Ok(new { id, url = $"/ReportBuilder/Edit/{id}" });
    }

    [HttpDelete("{id:int}")]
    [RequirePermissionCode(ReportConfigPermissions.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _configService.DeleteReportAsync(id, cancellationToken);
        return NoContent();
    }
}
