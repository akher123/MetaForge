using MetaForge.Domain.Features;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class VehicleInsuranceConfiguration : IEntityTypeConfiguration<VehicleInsurance>
{
    public void Configure(EntityTypeBuilder<VehicleInsurance> builder)
    {
        builder.ToTable("VehicleInsurances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.PolicyNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.PremiumAmount).HasPrecision(18, 2).IsRequired();
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
    }
}
