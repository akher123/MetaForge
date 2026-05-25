namespace MetaForge.Domain.Business;

/// <summary>
/// Sample master data entity.
/// </summary>
public class Country : BaseEntity, IForgeBusinessEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
