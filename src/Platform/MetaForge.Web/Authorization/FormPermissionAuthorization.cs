using Microsoft.AspNetCore.Mvc.Filters;

namespace MetaForge.Web.Authorization;

/// <summary>
/// Enforces an exact permission code (e.g. security.ViewUsers, config.Manage).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionCodeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permissionCode;

    public RequirePermissionCodeAttribute(string permissionCode) => _permissionCode = permissionCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var authService = context.HttpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var allowed = await authService.HasPermissionCodeAsync(context.HttpContext.User, _permissionCode);

        if (!allowed)
            context.Result = new ForbidResult();
    }
}

/// <summary>
/// Enforces form action permission from route formCode parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireFormPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _action;

    public string RouteKey { get; set; } = "formCode";

    public RequireFormPermissionAttribute(string action) => _action = action;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var formCode = context.RouteData.Values[RouteKey]?.ToString();
        if (string.IsNullOrWhiteSpace(formCode))
        {
            context.Result = new BadRequestObjectResult(new { error = "Form code is required for authorization." });
            return;
        }

        var authService = context.HttpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var allowed = await authService.HasFormPermissionAsync(context.HttpContext.User, formCode, _action);

        if (!allowed)
            context.Result = new ForbidResult();
    }
}

/// <summary>
/// Helper for entity-based API permission checks.
/// </summary>
public static class PermissionGuard
{
    public static async Task<IActionResult?> EnsureEntityPermissionAsync(
        HttpContext httpContext,
        string entityName,
        string action,
        CancellationToken cancellationToken = default)
    {
        var authService = httpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var formCode = await authService.ResolveFormCodeByEntityAsync(entityName, cancellationToken);

        if (string.IsNullOrWhiteSpace(formCode))
            return new BadRequestObjectResult(new { error = $"No form configured for entity '{entityName}'." });

        var allowed = await authService.HasFormPermissionAsync(httpContext.User, formCode, action, cancellationToken);
        return allowed ? null : new ForbidResult();
    }

    public static async Task<IActionResult?> EnsureFormPermissionAsync(
        HttpContext httpContext,
        string formCode,
        string action,
        CancellationToken cancellationToken = default)
    {
        var authService = httpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var allowed = await authService.HasFormPermissionAsync(httpContext.User, formCode, action, cancellationToken);
        return allowed ? null : new ForbidResult();
    }

    public static async Task<IActionResult?> EnsurePermissionCodeAsync(
        HttpContext httpContext,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var authService = httpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var allowed = await authService.HasPermissionCodeAsync(httpContext.User, permissionCode, cancellationToken);
        return allowed ? null : new ForbidResult();
    }

    public static async Task<IActionResult?> EnsureLookupAccessAsync(
        HttpContext httpContext,
        string entityName,
        CancellationToken cancellationToken = default)
    {
        var authService = httpContext.RequestServices.GetRequiredService<IFormAuthorizationService>();
        var allowed = await authService.CanAccessLookupAsync(httpContext.User, entityName, cancellationToken);
        return allowed ? null : new ForbidResult();
    }
}
