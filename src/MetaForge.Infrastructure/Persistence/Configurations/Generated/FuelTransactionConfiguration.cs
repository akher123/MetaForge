using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Features.Generated;

public class FuelTransactionConfiguration : IEntityTypeConfiguration<FuelTransaction>
{
    public void Configure(EntityTypeBuilder<FuelTransaction> builder)
    {
        builder.ToTable("FuelTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.FuelTypeId).IsRequired();
        builder.Property(x => x.FuelDate).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Odometer).HasPrecision(18, 2).IsRequired();
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
        builder.HasOne(x => x.FuelType).WithMany().HasForeignKey(x => x.FuelTypeId);
    }
}
