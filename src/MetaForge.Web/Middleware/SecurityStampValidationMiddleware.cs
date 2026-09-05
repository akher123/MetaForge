using System.Security.Claims;
using MetaForge.Application.Interfaces;
using MetaForge.Infrastructure.Persistence;
using MetaForge.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Web.Middleware;

/// <summary>
/// Validates the security stamp on each request and refreshes the auth cookie when roles or permissions change.
/// </summary>
public sealed class SecurityStampValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityStampValidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        MetaForgeDbContext dbContext,
        IUserClaimsFactory claimsFactory,
        IMemoryCache cache)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var userId))
            {
                var claimStamp = context.User.FindFirstValue(AppConstants.SecurityStampClaimType);
                var cacheKey = $"{AppConstants.SecurityStampCacheKeyPrefix}{userId}";

                if (cache.TryGetValue(cacheKey, out string? cachedStamp)
                    && !string.IsNullOrWhiteSpace(cachedStamp)
                    && string.Equals(claimStamp, cachedStamp, StringComparison.Ordinal))
                {
                    await _next(context);
                    return;
                }

                var dbStamp = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId && u.IsActive)
                    .Select(u => u.SecurityStamp)
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (string.IsNullOrWhiteSpace(dbStamp) || !string.Equals(claimStamp, dbStamp, StringComparison.Ordinal))
                {
                    cache.Remove(cacheKey);

                    if (string.IsNullOrWhiteSpace(dbStamp))
                    {
                        await context.SignOutAsync("Cookies");
                    }
                    else
                    {
                        var refreshedPrincipal = await claimsFactory.CreatePrincipalAsync(userId, context.RequestAborted);
                        if (refreshedPrincipal == null)
                        {
                            await context.SignOutAsync("Cookies");
                        }
                        else
                        {
                            var authResult = await context.AuthenticateAsync("Cookies");
                            var properties = authResult.Properties ?? new AuthenticationProperties();
                            await context.SignInAsync("Cookies", refreshedPrincipal, properties);
                            context.User = refreshedPrincipal;
                            claimStamp = dbStamp;
                        }
                    }
                }
                else
                {
                    cache.Set(cacheKey, dbStamp, new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromSeconds(60),
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
                }
            }
        }

        await _next(context);
    }
}
