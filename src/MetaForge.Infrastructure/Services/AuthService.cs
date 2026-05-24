using System.Security.Cryptography;
using System.Text;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Authenticates users against the security store.
/// </summary>
public class AuthService : IAuthService
{
    private readonly MetaForgeDbContext _dbContext;

    public AuthService(MetaForgeDbContext dbContext) => _dbContext = dbContext;

    public async Task<AuthResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive, cancellationToken);

        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            return null;

        return new AuthResult
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }
}

/// <summary>
/// Simple password hashing utility.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verify(string password, string hash) =>
        string.Equals(Hash(password), hash, StringComparison.Ordinal);
}
