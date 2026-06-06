using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Features.Generated;

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.ToTable("VehicleModels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleMakeId).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.VehicleMake).WithMany().HasForeignKey(x => x.VehicleMakeId);
    }
}
