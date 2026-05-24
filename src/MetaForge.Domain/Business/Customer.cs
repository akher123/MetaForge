namespace MetaForge.Domain.Business;

/// <summary>
/// Sample customer entity with one-to-one address.
/// </summary>
public class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Status { get; set; } = "Active";

    public int? CountryId { get; set; }

    public int? RegionId { get; set; }

    public Country? Country { get; set; }

    public Region? Region { get; set; }

    public Address? Address { get; set; }
}
