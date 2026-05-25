namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/security")]
public class SecurityApiController : ControllerBase
{
    private readonly ISecurityManagementService _securityService;

    public SecurityApiController(ISecurityManagementService securityService) => _securityService = securityService;

    [HttpGet("overview")]
    [RequirePermissionCode(SecurityPermissions.ViewUsers)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        Ok(await _securityService.GetOverviewAsync(cancellationToken));

    [HttpGet("users")]
    [RequirePermissionCode(SecurityPermissions.ViewUsers)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken) =>
        Ok(await _securityService.GetUsersAsync(cancellationToken));

    [HttpGet("users/{id:int}")]
    [RequirePermissionCode(SecurityPermissions.ViewUsers)]
    public async Task<IActionResult> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _securityService.GetUserForEditAsync(id, cancellationToken);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost("users")]
    [RequirePermissionCode(SecurityPermissions.ManageUsers)]
    public async Task<IActionResult> SaveUser([FromBody] SaveUserDto user, CancellationToken cancellationToken)
    {
        var id = await _securityService.SaveUserAsync(user, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("users/{id:int}")]
    [RequirePermissionCode(SecurityPermissions.ManageUsers)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        await _securityService.DeleteUserAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("roles")]
    [RequirePermissionCode(SecurityPermissions.ViewRoles)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        Ok(await _securityService.GetRolesAsync(cancellationToken));

    [HttpGet("roles/{id:int}")]
    [RequirePermissionCode(SecurityPermissions.ViewRoles)]
    public async Task<IActionResult> GetRole(int id, CancellationToken cancellationToken)
    {
        var role = await _securityService.GetRoleAsync(id, cancellationToken);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPost("roles")]
    [RequirePermissionCode(SecurityPermissions.ManageRoles)]
    public async Task<IActionResult> SaveRole([FromBody] SaveRoleDto role, CancellationToken cancellationToken)
    {
        var id = await _securityService.SaveRoleAsync(role, cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("roles/{id:int}")]
    [RequirePermissionCode(SecurityPermissions.ManageRoles)]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken cancellationToken)
    {
        await _securityService.DeleteRoleAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("permissions")]
    [RequirePermissionCode(SecurityPermissions.ViewPermissions)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await _securityService.GetPermissionGroupsAsync(cancellationToken));

    [HttpPost("permissions/sync")]
    [RequirePermissionCode(SecurityPermissions.SyncPermissions)]
    public async Task<IActionResult> SyncPermissions(CancellationToken cancellationToken)
    {
        var added = await _securityService.SyncFormPermissionsAsync(cancellationToken);
        return Ok(new { added, message = $"{added} permission(s) synced." });
    }
}
