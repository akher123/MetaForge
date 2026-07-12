namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/audit")]
public class AuditApiController : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService;

    public AuditApiController(IAuditQueryService auditQueryService) => _auditQueryService = auditQueryService;

    [HttpGet]
    [RequirePermissionCode(SecurityPermissions.ViewAudit)]
    public async Task<IActionResult> GetLogs([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
        Ok(await _auditQueryService.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:long}")]
    [RequirePermissionCode(SecurityPermissions.ViewAudit)]
    public async Task<IActionResult> GetDetail(long id, CancellationToken cancellationToken)
    {
        var detail = await _auditQueryService.GetDetailAsync(id, cancellationToken);
        return detail == null ? NotFound() : Ok(detail);
    }

    [HttpGet("entities")]
    [RequirePermissionCode(SecurityPermissions.ViewAudit)]
    public async Task<IActionResult> GetEntities(CancellationToken cancellationToken) =>
        Ok(await _auditQueryService.GetEntityOptionsAsync(cancellationToken));

    [HttpGet("actions")]
    [RequirePermissionCode(SecurityPermissions.ViewAudit)]
    public async Task<IActionResult> GetActions(CancellationToken cancellationToken) =>
        Ok(await _auditQueryService.GetActionOptionsAsync(cancellationToken));
}
