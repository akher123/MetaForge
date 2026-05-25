namespace MetaForge.Domain.Business;

/// <summary>
/// One-to-one related address for customer.
/// </summary>
public class Address : BaseEntity, IForgeBusinessEntity
{
    public int CustomerId { get; set; }

    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public int? CountryId { get; set; }

    public Customer Customer { get; set; } = null!;

    public Country? Country { get; set; }
}
