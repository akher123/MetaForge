namespace MetaForge.Domain.Features;

/// <summary>
/// MaintenanceType business entity (scaffolded from MaintenanceTypes).
/// </summary>
public class MaintenanceType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
