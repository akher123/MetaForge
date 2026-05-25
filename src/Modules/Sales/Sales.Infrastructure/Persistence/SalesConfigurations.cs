using MetaForge.Domain.Business;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Modules.Sales.Persistence;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNo).HasMaxLength(50).IsRequired();
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasMany(x => x.Items).WithOne(x => x.SalesOrder).HasForeignKey(x => x.SalesOrderId);
        builder.HasMany(x => x.Charges).WithOne(x => x.SalesOrder).HasForeignKey(x => x.SalesOrderId);
    }
}

public class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
    }
}

public class SalesOrderChargeConfiguration : IEntityTypeConfiguration<SalesOrderCharge>
{
    public void Configure(EntityTypeBuilder<SalesOrderCharge> builder)
    {
        builder.ToTable("SalesOrderCharges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChargeType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
    }
}
