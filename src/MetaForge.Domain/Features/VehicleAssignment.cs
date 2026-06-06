namespace MetaForge.Domain.Features;

/// <summary>
/// VehicleAssignment business entity (scaffolded from VehicleAssignments).
/// </summary>
public class VehicleAssignment : BaseEntity
{
    public int VehicleId { get; set; }
    public int DriverId { get; set; }
    public DateTime AssignedDate { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public string? AssignmentReason { get; set; }
    public Vehicle? Vehicle { get; set; }
}
