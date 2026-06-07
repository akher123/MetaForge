namespace MetaForge.Domain.Features;

/// <summary>
/// MaintenanceRecord business entity (scaffolded from MaintenanceRecords).
/// </summary>
public class MaintenanceRecord : BaseEntity
{
    public int VehicleId { get; set; }
    public int MaintenanceTypeId { get; set; }
    public DateTime ServiceDate { get; set; }
    public decimal Odometer { get; set; }
    public decimal Cost { get; set; }
    public int VendorId { get; set; }
    public string? Notes { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public decimal? NextServiceOdometer { get; set; }
    public Vehicle? Vehicle { get; set; }
    public MaintenanceType? MaintenanceType { get; set; }
    public Vendor? Vendor { get; set; }
}
