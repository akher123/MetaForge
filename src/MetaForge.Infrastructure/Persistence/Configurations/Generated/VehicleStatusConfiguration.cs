using MetaForge.Domain.Features;

namespace MetaForge.Infrastructure.Persistence.Configurations.Features.Generated;

public class VehicleStatusConfiguration : IEntityTypeConfiguration<VehicleStatus>
{
    public void Configure(EntityTypeBuilder<VehicleStatus> builder)
    {
        builder.ToTable("VehicleStatus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }
}
