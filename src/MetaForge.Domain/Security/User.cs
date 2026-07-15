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

    /// <summary>UI theme override; null inherits system default.</summary>
    public string? ThemeKey { get; set; }

    /// <summary>Culture override (e.g. en-US, ar-SA); null inherits system default.</summary>
    public string? CultureOverride { get; set; }

    /// <summary>Date display format override; null inherits system default.</summary>
    public string? DateFormatOverride { get; set; }

    /// <summary>Date-time display format override; null inherits system default.</summary>
    public string? DateTimeFormatOverride { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
