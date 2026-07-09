namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for admin form metadata configuration.
/// </summary>
[Authorize]
public class FormConfigController : Controller
{
    private readonly IFormConfigurationService _configService;
    private readonly IFormHealthCheckService _healthCheckService;

    public FormConfigController(
        IFormConfigurationService configService,
        IFormHealthCheckService healthCheckService)
    {
        _configService = configService;
        _healthCheckService = healthCheckService;
    }

    [HttpGet("/ModuleConfig")]
    [HttpGet("/FormBuilder")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Forms = await _configService.GetAllFormsAsync(cancellationToken);
        return View();
    }

    [HttpGet("/ModuleConfig/Create")]
    [HttpGet("/FormBuilder/Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Entities = await _configService.GetDiscoveredEntitiesAsync(cancellationToken);
        ViewBag.IsEdit = false;
        return View("Form");
    }

    [HttpGet("/FormBuilder/Health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.HealthReport = await _healthCheckService.GetReportAsync(cancellationToken);
        return View("Health");
    }

    [HttpGet("/ModuleConfig/Edit/{id:int}")]
    [HttpGet("/FormBuilder/Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, ConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        var module = await _configService.GetFormAsync(id, cancellationToken);
        if (module == null) return NotFound();

        ViewBag.Entities = await _configService.GetDiscoveredEntitiesAsync(cancellationToken);
        ViewBag.IsEdit = true;
        ViewBag.Form = module;
        ViewBag.Screen = await _configService.GetScreenAsync(id, cancellationToken);
        return View("Form");
    }
}
