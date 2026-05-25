using MetaForge.Domain.Security;
using Microsoft.AspNetCore.Identity;
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
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive, cancellationToken);

        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            return null;

        if (PasswordHasher.IsLegacyHash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(password);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

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
/// Password hashing with ASP.NET Core Identity (PBKDF2 + per-user salt).
/// Supports legacy SHA256 hashes for migration.
/// </summary>
public static class PasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(string password) =>
        Hasher.HashPassword(null!, password);

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        if (IsLegacyHash(storedHash))
            return VerifyLegacySha256(password, storedHash);

        return Hasher.VerifyHashedPassword(null!, storedHash, password)
            != PasswordVerificationResult.Failed;
    }

    public static bool IsLegacyHash(string storedHash)
    {
        if (storedHash is "admin")
            return true;

        if (storedHash.Length == 44 && !storedHash.StartsWith("AQAAAA", StringComparison.Ordinal))
        {
            try
            {
                Convert.FromBase64String(storedHash);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var computed = Convert.ToBase64String(bytes);
        return string.Equals(computed, storedHash, StringComparison.Ordinal);
    }
}
