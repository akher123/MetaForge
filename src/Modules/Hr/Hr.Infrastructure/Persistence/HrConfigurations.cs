using MetaForge.Domain.Business;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Modules.Hr.Persistence;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DepartmentId).IsRequired();
        builder.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.DateOfBirth).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.AdmissionDate).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Department)
            .WithMany(d => d.Students)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DepartmentCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Description).HasMaxLength(500);
        builder.Property(d => d.HeadOfDepartment).HasMaxLength(100);
        builder.Property(d => d.ContactEmail).HasMaxLength(100);
        builder.Property(d => d.ContactPhone).HasMaxLength(20);
        builder.Property(d => d.EstablishedDate).IsRequired();
        builder.Property(d => d.IsActive).HasDefaultValue(true);
        builder.HasMany(d => d.Students)
            .WithOne(s => s.Department)
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
