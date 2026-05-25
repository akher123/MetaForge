using System.Security.Claims;
using MetaForge.Application.Authorization;
using MetaForge.Application.Interfaces;
using MetaForge.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Loads authorization snapshots from the database, cached by user id and security stamp.
/// </summary>
public sealed class UserAuthorizationSnapshotProvider : IUserAuthorizationSnapshotProvider
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public UserAuthorizationSnapshotProvider(MetaForgeDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<UserAuthorizationSnapshot?> GetSnapshotAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
            return null;

        var stamp = user.FindFirstValue(AppConstants.SecurityStampClaimType);
        if (string.IsNullOrWhiteSpace(stamp))
            return null;

        var cacheKey = $"{AppConstants.AuthorizationSnapshotCacheKeyPrefix}{userId}:{stamp}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            var isActive = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == stamp, cancellationToken);

            if (!isActive)
                return null;

            var roles = await _dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync(cancellationToken);

            var permissions = await _dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync(cancellationToken);

            return new UserAuthorizationSnapshot
            {
                Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
                Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase)
            };
        });
    }
}
