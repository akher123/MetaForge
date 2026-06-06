namespace MetaForge.Domain.Features;

/// <summary>
/// DriverLicense business entity (scaffolded from DriverLicenses).
/// </summary>
public class DriverLicense : BaseEntity
{
    public int DriverId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? IssuedBy { get; set; }
    public string? Notes { get; set; }
    public Driver? Driver { get; set; }
}
