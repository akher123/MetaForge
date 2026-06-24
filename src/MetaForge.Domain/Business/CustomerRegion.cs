namespace MetaForge.Domain.Business;

/// <summary>
/// Junction entity linking customers to multiple regions.
/// </summary>
public class CustomerRegion
{
    public int CustomerId { get; set; }

    public int RegionId { get; set; }

    public Customer Customer { get; set; } = null!;

    public Region Region { get; set; } = null!;
}
