using MetaForge.Domain.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("Semesters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SemesterName).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.AcademicYear).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Term).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CreatedAt);
    }
}
