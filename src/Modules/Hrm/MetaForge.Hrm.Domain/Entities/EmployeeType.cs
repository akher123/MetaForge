namespace MetaForge.Hrm.Domain.Entities;

/// <summary>
/// EmployeeType entity for module Hrm (schema: hrm).
/// </summary>
public class EmployeeType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
}
