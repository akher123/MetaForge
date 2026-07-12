namespace MetaForge.Domain.Business;

/// <summary>
/// City within a region — third level of the location tree sample (Country → Region → City).
/// </summary>
public class City : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int RegionId { get; set; }

    public Region Region { get; set; } = null!;
}
