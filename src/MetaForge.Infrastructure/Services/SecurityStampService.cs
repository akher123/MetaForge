using MetaForge.Application.Interfaces;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Rotates security stamps when roles or permissions change.
/// </summary>
public sealed class SecurityStampService : ISecurityStampService
{
    private readonly MetaForgeDbContext _dbContext;

    public SecurityStampService(MetaForgeDbContext dbContext) => _dbContext = dbContext;

    public string GenerateStamp() => Guid.NewGuid().ToString("N");

    public async Task EnsureUserHasStampAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || !string.IsNullOrWhiteSpace(user.SecurityStamp))
            return;

        user.SecurityStamp = GenerateStamp();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BumpUserStampAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            return;

        user.SecurityStamp = GenerateStamp();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BumpUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var userIds = await _dbContext.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return;

        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        foreach (var user in users)
            user.SecurityStamp = GenerateStamp();

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
