namespace MetaForge.Domain.Business;

public class Product : BaseEntity, IForgeBusinessEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;
}
