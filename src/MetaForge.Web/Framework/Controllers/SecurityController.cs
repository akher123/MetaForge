namespace MetaForge.Web.Controllers;

[Authorize]
public class SecurityController : Controller
{
    private readonly ISecurityManagementService _securityService;
    private readonly IFormAuthorizationService _authorizationService;

    public SecurityController(
        ISecurityManagementService securityService,
        IFormAuthorizationService authorizationService)
    {
        _securityService = securityService;
        _authorizationService = authorizationService;
    }

    [HttpGet("/Security")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denied = await EnsureAnySecurityViewAsync(cancellationToken);
        if (denied != null) return denied;

        ViewBag.Overview = await _securityService.GetOverviewAsync(cancellationToken);
        ViewBag.CanViewUsers = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewUsers, cancellationToken);
        ViewBag.CanManageUsers = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ManageUsers, cancellationToken);
        ViewBag.CanViewRoles = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewRoles, cancellationToken);
        ViewBag.CanManageRoles = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ManageRoles, cancellationToken);
        ViewBag.CanViewPermissions = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewPermissions, cancellationToken);
        ViewBag.CanSyncPermissions = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.SyncPermissions, cancellationToken);
        ViewBag.CanViewAudit = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewAudit, cancellationToken);
        return View();
    }

    [HttpGet("/Security/Users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ViewUsers, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Users = await _securityService.GetUsersAsync(cancellationToken);
        ViewBag.CanManageUsers = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ManageUsers, cancellationToken);
        return View();
    }

    [HttpGet("/Security/Users/Create")]
    public async Task<IActionResult> CreateUser(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ManageUsers, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Roles = await _securityService.GetRoleOptionsAsync(cancellationToken);
        ViewBag.IsEdit = false;
        return View("UserForm");
    }

    [HttpGet("/Security/Users/Edit/{id:int}")]
    public async Task<IActionResult> EditUser(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ManageUsers, cancellationToken);
        if (denied != null) return denied;

        var user = await _securityService.GetUserForEditAsync(id, cancellationToken);
        if (user == null) return NotFound();

        ViewBag.Roles = await _securityService.GetRoleOptionsAsync(cancellationToken);
        ViewBag.IsEdit = true;
        ViewBag.User = user;
        return View("UserForm");
    }

    [HttpGet("/Security/Roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ViewRoles, cancellationToken);
        if (denied != null) return denied;

        ViewBag.Roles = await _securityService.GetRolesAsync(cancellationToken);
        ViewBag.CanManageRoles = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ManageRoles, cancellationToken);
        return View();
    }

    [HttpGet("/Security/Roles/Create")]
    public async Task<IActionResult> CreateRole(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ManageRoles, cancellationToken);
        if (denied != null) return denied;

        var groups = await _securityService.GetPermissionGroupsAsync(cancellationToken);
        ViewBag.IsEdit = false;
        ViewBag.Role = new MetaForge.Application.DTOs.RoleDetailDto { PermissionGroups = groups.ToList() };
        return View("RoleForm");
    }

    [HttpGet("/Security/Roles/Edit/{id:int}")]
    public async Task<IActionResult> EditRole(int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ManageRoles, cancellationToken);
        if (denied != null) return denied;

        var role = await _securityService.GetRoleAsync(id, cancellationToken);
        if (role == null) return NotFound();

        ViewBag.IsEdit = true;
        ViewBag.Role = role;
        return View("RoleForm");
    }

    [HttpGet("/Security/Permissions")]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ViewPermissions, cancellationToken);
        if (denied != null) return denied;

        ViewBag.PermissionGroups = await _securityService.GetPermissionGroupsAsync(cancellationToken);
        ViewBag.CanSyncPermissions = await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.SyncPermissions, cancellationToken);
        return View();
    }

    [HttpGet("/Security/Audit")]
    public async Task<IActionResult> Audit(CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsurePermissionCodeAsync(HttpContext, SecurityPermissions.ViewAudit, cancellationToken);
        if (denied != null) return denied;

        return View();
    }

    private async Task<IActionResult?> EnsureAnySecurityViewAsync(CancellationToken cancellationToken)
    {
        if (await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewUsers, cancellationToken)
            || await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewRoles, cancellationToken)
            || await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewPermissions, cancellationToken)
            || await _authorizationService.HasPermissionCodeAsync(User, SecurityPermissions.ViewAudit, cancellationToken))
        {
            return null;
        }

        return new ForbidResult();
    }
}
