namespace MetaForge.Domain.Business;

/// <summary>
/// Additional charges on a sales order (freight, tax, discount, etc.).
/// </summary>
public class SalesOrderCharge : BaseEntity
{
    public int SalesOrderId { get; set; }

    public string ChargeType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Amount { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
}
