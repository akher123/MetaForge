namespace MetaForge.Domain.Features;

/// <summary>
/// Driver business entity (scaffolded from Drivers).
/// </summary>
public class Driver : BaseEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MobileNo { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
