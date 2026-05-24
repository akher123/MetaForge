namespace MetaForge.Domain.Business;

public class Student : BaseEntity
{
    public string StudentCode { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateTime AdmissionDate { get; set; }

    public bool IsActive { get; set; } = true;
    public Department? Department { get; set; }
}