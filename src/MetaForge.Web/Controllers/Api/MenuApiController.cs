namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/menus")]
public class MenuApiController : ControllerBase
{
    private readonly IMenuManagementService _menuService;
    private readonly INavigationService _navigationService;

    public MenuApiController(IMenuManagementService menuService, INavigationService navigationService)
    {
        _menuService = menuService;
        _navigationService = navigationService;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken) =>
        Ok(await _navigationService.GetSidebarMenuAsync(cancellationToken));

    [HttpGet]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _menuService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var entry = await _menuService.GetAsync(id, cancellationToken);
        return entry == null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> Save([FromBody] MenuEntryDto entry, CancellationToken cancellationToken)
    {
        var id = await _menuService.SaveAsync(entry, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("{id:int}")]
    [RequirePermissionCode(ConfigPermissions.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _menuService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
