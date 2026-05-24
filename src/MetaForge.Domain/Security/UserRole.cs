namespace MetaForge.Domain.Security;

/// <summary>
/// Join entity between users and roles.
/// </summary>
public class UserRole
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
