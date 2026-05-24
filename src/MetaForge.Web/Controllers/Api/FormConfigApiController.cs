namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/formconfig")]
public class FormConfigApiController : ControllerBase
{
    private readonly IFormConfigurationService _configService;

    public FormConfigApiController(IFormConfigurationService configService) => _configService = configService;

    [HttpGet]
    [RequirePermissionCode(ConfigPermissions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _configService.GetAllFormsAsync(cancellationToken));

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
