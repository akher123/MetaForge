namespace MetaForge.Domain.Business;

/// <summary>
/// Teacher business entity (scaffolded from Teachers).
/// </summary>
public class Teacher : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? JoiningDate { get; set; }
    public decimal? Salary { get; set; }
}
