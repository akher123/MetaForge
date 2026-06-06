using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.DriverId).IsRequired();
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime);
        builder.Property(x => x.StartOdometer).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.EndOdometer).HasPrecision(18, 2);
        builder.Property(x => x.StartLocation).HasMaxLength(300);
        builder.Property(x => x.EndLocation).HasMaxLength(300);
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
    }
}
