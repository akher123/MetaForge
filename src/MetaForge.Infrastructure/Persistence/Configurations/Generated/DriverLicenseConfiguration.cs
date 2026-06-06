using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class DriverLicenseConfiguration : IEntityTypeConfiguration<DriverLicense>
{
    public void Configure(EntityTypeBuilder<DriverLicense> builder)
    {
        builder.ToTable("DriverLicenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DriverId).IsRequired();
        builder.Property(x => x.LicenseNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IssueDate).IsRequired();
        builder.Property(x => x.ExpiryDate).IsRequired();
        builder.Property(x => x.IssuedBy).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
    }
}
