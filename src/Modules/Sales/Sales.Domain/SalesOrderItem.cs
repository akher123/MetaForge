namespace MetaForge.Domain.Business;

/// <summary>
/// Sample detail entity for one-to-many master-detail.
/// </summary>
public class SalesOrderItem : BaseEntity, IForgeBusinessEntity
{
    public int SalesOrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
