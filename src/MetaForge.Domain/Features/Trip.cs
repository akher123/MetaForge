namespace MetaForge.Domain.Features;

/// <summary>
/// Trip business entity (scaffolded from Trips).
/// </summary>
public class Trip : BaseEntity
{
    public int VehicleId { get; set; }
    public int DriverId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal StartOdometer { get; set; }
    public decimal? EndOdometer { get; set; }
    public string? StartLocation { get; set; }
    public string? EndLocation { get; set; }
    public string? Purpose { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Driver? Driver { get; set; }
}
