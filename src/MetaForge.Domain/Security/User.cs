namespace MetaForge.Domain.Security;

/// <summary>
/// Application user for RBAC.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? SecurityStamp { get; set; }

    /// <summary>UI theme key (e.g. indigo-light, indigo-dark).</summary>
    public string ThemeKey { get; set; } = "indigo-light";

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
