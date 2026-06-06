namespace MetaForge.Domain.Features;

/// <summary>
/// VehicleModel business entity (scaffolded from VehicleModels).
/// </summary>
public class VehicleModel : BaseEntity
{
    public int VehicleMakeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public VehicleMake? VehicleMake { get; set; }
}
