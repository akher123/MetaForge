namespace MetaForge.Hrm.Domain.Entities;

/// <summary>
/// Employee entity for module Hrm (schema: hrm).
/// </summary>
public class Employee : BaseEntity
{
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; } = 0;
    public bool IsActive { get; set; } = false;
    public Department? Department { get; set; }
}
