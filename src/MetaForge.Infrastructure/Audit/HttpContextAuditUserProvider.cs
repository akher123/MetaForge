using MetaForge.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MetaForge.Infrastructure.Audit;

/// <summary>
/// Resolves the audit user from the current HTTP request principal.
/// </summary>
public sealed class HttpContextAuditUserProvider : IAuditUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditUserProvider(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public string GetCurrentUserName() =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
}
