namespace MetaForge.Domain.Business;

public class Supplier : BaseEntity, IForgeBusinessEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }

    public bool IsActive { get; set; } = true;
}
