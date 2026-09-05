namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for navigation menu administration.
/// </summary>
[Authorize]
public class MenuController : Controller
{
    private readonly IMenuManagementService _menuService;

    public MenuController(IMenuManagementService menuService) => _menuService = menuService;

    [HttpGet("/Menu")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Menus = await _menuService.GetAllAsync(cancellationToken);
        return View();
    }

    [HttpGet("/Menu/Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        await LoadFormOptionsAsync(cancellationToken);
        ViewBag.IsEdit = false;
        return View("Form");
    }

    [HttpGet("/Menu/Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        var entry = await _menuService.GetAsync(id, cancellationToken);
        if (entry == null) return NotFound();

        await LoadFormOptionsAsync(cancellationToken, id);
        ViewBag.IsEdit = true;
        ViewBag.Entry = entry;
        return View("Form");
    }

    private async Task LoadFormOptionsAsync(CancellationToken cancellationToken, int? excludeId = null)
    {
        ViewBag.ParentOptions = await _menuService.GetFolderOptionsAsync(excludeId, cancellationToken);
        ViewBag.FormOptions = await _menuService.GetFormOptionsAsync(cancellationToken);
    }
}
