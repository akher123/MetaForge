namespace MetaForge.Web.Controllers;

/// <summary>
/// MVC controller for email configuration admin screens.
/// </summary>
[Authorize]
public class EmailConfigController : Controller
{
    private readonly IEmailConfigurationService _configService;
    private readonly IEmailMessageService _messageService;

    public EmailConfigController(
        IEmailConfigurationService configService,
        IEmailMessageService messageService)
    {
        _configService = configService;
        _messageService = messageService;
    }

    [HttpGet("/EmailAdmin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Channels = await _configService.GetChannelsAsync(cancellationToken);
        ViewBag.Templates = await _configService.GetTemplatesAsync(cancellationToken);
        ViewBag.Policies = await _configService.GetRetryPoliciesAsync(cancellationToken);
        return View();
    }

    [HttpGet("/EmailAdmin/Channels")]
    public async Task<IActionResult> Channels(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Channels = await _configService.GetChannelsAsync(cancellationToken);
        return View();
    }

    [HttpGet("/EmailAdmin/RetryPolicies")]
    public async Task<IActionResult> RetryPolicies(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Policies = await _configService.GetRetryPoliciesAsync(cancellationToken);
        return View();
    }

    [HttpGet("/EmailAdmin/Templates")]
    public async Task<IActionResult> Templates(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Templates = await _configService.GetTemplatesAsync(cancellationToken);
        return View();
    }

    [HttpGet("/EmailAdmin/Templates/Create")]
    public async Task<IActionResult> CreateTemplate(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        ViewBag.IsEdit = false;
        ViewBag.Channels = await _configService.GetChannelsAsync(cancellationToken);
        ViewBag.Policies = await _configService.GetRetryPoliciesAsync(cancellationToken);
        ViewBag.Forms = await _configService.GetFormOptionsAsync(cancellationToken);
        return View("TemplateForm");
    }

    [HttpGet("/EmailAdmin/Templates/Edit/{id:int}")]
    public async Task<IActionResult> EditTemplate(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.Manage, cancellationToken);
        if (denied != null) return denied;

        var template = await _configService.GetTemplateAsync(id, cancellationToken);
        if (template == null) return NotFound();

        ViewBag.IsEdit = true;
        ViewBag.Template = template;
        ViewBag.Channels = await _configService.GetChannelsAsync(cancellationToken);
        ViewBag.Policies = await _configService.GetRetryPoliciesAsync(cancellationToken);
        ViewBag.Forms = await _configService.GetFormOptionsAsync(cancellationToken);
        return View("TemplateForm");
    }

    [HttpGet("/EmailAdmin/Log")]
    public async Task<IActionResult> Log(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, EmailConfigPermissions.View, cancellationToken);
        if (denied != null) return denied;

        var messages = await _messageService.GetMessagesAsync(new EmailLogQuery { Page = 1, PageSize = 50 }, cancellationToken);
        ViewBag.Messages = messages;
        return View();
    }
}
