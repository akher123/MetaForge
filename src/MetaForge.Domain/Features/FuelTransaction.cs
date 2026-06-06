namespace MetaForge.Domain.Features;

/// <summary>
/// FuelTransaction business entity (scaffolded from FuelTransactions).
/// </summary>
public class FuelTransaction : BaseEntity
{
    public int VehicleId { get; set; }
    public int FuelTypeId { get; set; }
    public DateTime FuelDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Odometer { get; set; }
    public Vehicle? Vehicle { get; set; }
    public FuelType? FuelType { get; set; }
}
