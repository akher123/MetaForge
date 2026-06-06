namespace MetaForge.Domain.Features;

/// <summary>
/// VehicleInsurance business entity (scaffolded from VehicleInsurances).
/// </summary>
public class VehicleInsurance : BaseEntity
{
    public int VehicleId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public Vehicle? Vehicle { get; set; }
}
