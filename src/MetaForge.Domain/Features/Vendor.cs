namespace MetaForge.Domain.Features;

/// <summary>
/// Vendor business entity (scaffolded from Vendors).
/// </summary>
public class Vendor : BaseEntity
{
    public string VendorName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
