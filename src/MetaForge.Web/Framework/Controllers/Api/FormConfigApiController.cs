namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/formconfig")]
public class FormConfigApiController : ControllerBase
{
    private readonly IFormConfigurationService _configService;
    private readonly IFormHealthCheckService _healthCheckService;

    public FormConfigApiController(
        IFormConfigurationService configService,
        IFormHealthCheckService healthCheckService)
    {
        _configService = configService;
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _configService.GetAllFormsAsync(cancellationToken));

    [HttpGet("validation-rules")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public IActionResult GetValidationRuleCatalog() =>
        Ok(ValidationRuleCatalog.GetAll());

    [HttpGet("conditional-rules")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public IActionResult GetConditionalRuleCatalog() =>
        Ok(new
        {
            actions = ConditionalRuleCatalog.GetActions(),
            operators = ConditionalRuleCatalog.GetOperators()
        });

    [HttpGet("health")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetHealthReport(CancellationToken cancellationToken) =>
        Ok(await _healthCheckService.GetReportAsync(cancellationToken));

    [HttpGet("health/{id:int}")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetFormHealth(int id, CancellationToken cancellationToken)
    {
        var item = await _healthCheckService.GetFormHealthAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("sync-preview/{id:int}")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetSchemaSyncPreview(int id, CancellationToken cancellationToken) =>
        Ok(await _configService.GetSchemaSyncPreviewAsync(id, cancellationToken));

    [HttpPost("sync/{id:int}")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> ApplySchemaSync(int id, [FromBody] FormSchemaSyncApplyDto request, CancellationToken cancellationToken) =>
        Ok(await _configService.ApplySchemaSyncAsync(id, request, cancellationToken));

    [HttpGet("{id:int}")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var module = await _configService.GetFormAsync(id, cancellationToken);
        return module == null ? NotFound() : Ok(module);
    }

    [HttpGet("screen/{id:int}")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetScreen(int id, CancellationToken cancellationToken) =>
        Ok(await _configService.GetScreenAsync(id, cancellationToken));

    [HttpGet("by-entity/{entityName}")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetByEntity(string entityName, CancellationToken cancellationToken)
    {
        var module = await _configService.GetFormByEntityAsync(entityName, cancellationToken);
        return module == null ? NotFound() : Ok(module);
    }

    [HttpGet("discovered")]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetDiscovered(CancellationToken cancellationToken) =>
        Ok(await _configService.GetDiscoveredEntitiesAsync(cancellationToken));

    [HttpGet("draft/{entityName}")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> BuildDraft(string entityName, [FromQuery] string groupName = "Master Data", CancellationToken cancellationToken = default) =>
        Ok(await _configService.BuildDraftAsync(entityName, groupName, cancellationToken));

    [HttpPost]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> Save([FromBody] FormConfigDto config, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveFormAsync(config, cancellationToken);
        return Ok(new { id, url = $"/Modules/{config.Code}" });
    }

    [HttpPost("screen")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> SaveScreen([FromBody] FormBuilderSaveDto screen, CancellationToken cancellationToken)
    {
        var id = await _configService.SaveScreenAsync(screen, cancellationToken);
        return Ok(new { id, url = $"/Modules/{screen.Master.Code}" });
    }

    [HttpDelete("{id:int}")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _configService.DeleteFormAsync(id, cancellationToken);
        return NoContent();
    }
}
