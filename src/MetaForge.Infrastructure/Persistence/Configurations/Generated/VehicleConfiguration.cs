using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Features.Generated;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.EngineNumber).HasMaxLength(50);
        builder.Property(x => x.VehicleTypeId).IsRequired();
        builder.Property(x => x.VehicleMakeId).IsRequired();
        builder.Property(x => x.VehicleModelId).IsRequired();
        builder.Property(x => x.ManufactureYear);
        builder.Property(x => x.PurchaseDate);
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.Property(x => x.CurrentOdometer).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VehicleStatusId).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.HasOne(x => x.VehicleType).WithMany().HasForeignKey(x => x.VehicleTypeId);
        builder.HasOne(x => x.VehicleMake).WithMany().HasForeignKey(x => x.VehicleMakeId);
        builder.HasOne(x => x.VehicleModel).WithMany().HasForeignKey(x => x.VehicleModelId);
        builder.HasOne(x => x.VehicleStatus).WithMany().HasForeignKey(x => x.VehicleStatusId);
    }
}
