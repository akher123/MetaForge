namespace MetaForge.Domain.Features;

/// <summary>
/// FuelType business entity (scaffolded from FuelTypes).
/// </summary>
public class FuelType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
