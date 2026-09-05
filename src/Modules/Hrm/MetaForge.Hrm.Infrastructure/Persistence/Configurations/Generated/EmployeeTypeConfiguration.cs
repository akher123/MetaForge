using MetaForge.Hrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Hrm.Infrastructure.Persistence.Configurations.Generated;

public class EmployeeTypeConfiguration : IEntityTypeConfiguration<EmployeeType>
{
    public void Configure(EntityTypeBuilder<EmployeeType> builder)
    {
        builder.ToTable("EmployeeTypes", "hrm");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
    }
}
