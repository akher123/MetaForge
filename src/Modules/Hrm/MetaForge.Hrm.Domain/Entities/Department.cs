namespace MetaForge.Hrm.Domain.Entities;

/// <summary>
/// Department entity for module Hrm (schema: hrm).
/// </summary>
public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
}
