using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> builder)
    {
        builder.ToTable("MaintenanceRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.MaintenanceTypeId).IsRequired();
        builder.Property(x => x.ServiceDate).IsRequired();
        builder.Property(x => x.Odometer).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Cost).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VendorId).IsRequired();
        builder.Property(x => x.Notes);
        builder.Property(x => x.NextServiceDate);
        builder.Property(x => x.NextServiceOdometer).HasPrecision(18, 2);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
        builder.HasOne(x => x.MaintenanceType).WithMany().HasForeignKey(x => x.MaintenanceTypeId);
        builder.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId);
    }
}
