namespace MetaForge.Domain.Business;

/// <summary>
/// Region/state within a country — used for cascading Country → Region lookups.
/// </summary>
public class Region : BaseEntity, IForgeBusinessEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int CountryId { get; set; }

    public Country Country { get; set; } = null!;
}
