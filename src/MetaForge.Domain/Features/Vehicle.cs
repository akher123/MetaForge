namespace MetaForge.Domain.Features;

/// <summary>
/// Vehicle business entity (scaffolded from Vehicles).
/// </summary>
public class Vehicle : BaseEntity
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? EngineNumber { get; set; }
    public int VehicleTypeId { get; set; }
    public int VehicleMakeId { get; set; }
    public int VehicleModelId { get; set; }
    public short? ManufactureYear { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal CurrentOdometer { get; set; }
    public int VehicleStatusId { get; set; }
    public bool IsDeleted { get; set; }
    public VehicleType? VehicleType { get; set; }
    public VehicleMake? VehicleMake { get; set; }
    public VehicleModel? VehicleModel { get; set; }
    public VehicleStatus? VehicleStatus { get; set; }
}
