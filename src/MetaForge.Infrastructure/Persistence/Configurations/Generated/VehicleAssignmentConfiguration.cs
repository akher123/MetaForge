using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("VehicleAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.DriverId).IsRequired();
        builder.Property(x => x.AssignedDate).IsRequired();
        builder.Property(x => x.ReleasedDate);
        builder.Property(x => x.AssignmentReason).HasMaxLength(500);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
    }
}
