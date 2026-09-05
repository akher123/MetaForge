using System.Security.Claims;
using MetaForge.Application.Interfaces;
using MetaForge.Shared.Constants;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Builds authentication principals with identity, roles, and security stamp only.
/// Permissions are resolved from the database on each authorization check.
/// </summary>
public sealed class UserClaimsFactory : IUserClaimsFactory
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly ISecurityStampService _securityStampService;

    public UserClaimsFactory(MetaForgeDbContext dbContext, ISecurityStampService securityStampService)
    {
        _dbContext = dbContext;
        _securityStampService = securityStampService;
    }

    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
            return null;

        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            await _securityStampService.EnsureUserHasStampAsync(userId, cancellationToken);
            user.SecurityStamp = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstAsync(cancellationToken);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(AppConstants.SecurityStampClaimType, user.SecurityStamp ?? string.Empty)
        };

        claims.AddRange(user.UserRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Role.Name)));

        var identity = new ClaimsIdentity(claims, "Cookies");
        return new ClaimsPrincipal(identity);
    }
}
