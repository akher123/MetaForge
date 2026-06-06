using MetaForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ToTable("VehicleDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VehicleId).IsRequired();
        builder.Property(x => x.DocumentTypeId).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(100);
        builder.Property(x => x.IssueDate);
        builder.Property(x => x.ExpiryDate);
        builder.Property(x => x.FilePath).HasMaxLength(1000);
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
        builder.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId);
    }
}
