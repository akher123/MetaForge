namespace MetaForge.Domain.Business;

/// <summary>
/// Sample master entity for one-to-many master-detail.
/// </summary>
public class SalesOrder : BaseEntity
{
    public string OrderNo { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public int CustomerId { get; set; }

    public string Status { get; set; } = "Draft";

    public Customer Customer { get; set; } = null!;

    public ICollection<SalesOrderItem> Items { get; set; } = [];

    public ICollection<SalesOrderCharge> Charges { get; set; } = [];
}
