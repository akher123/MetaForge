namespace MetaForge.Domain.Features;

/// <summary>
/// VehicleDocument business entity (scaffolded from VehicleDocuments).
/// </summary>
public class VehicleDocument : BaseEntity
{
    public int VehicleId { get; set; }
    public int DocumentTypeId { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? FilePath { get; set; }
    public Vehicle? Vehicle { get; set; }
    public DocumentType? DocumentType { get; set; }
}
