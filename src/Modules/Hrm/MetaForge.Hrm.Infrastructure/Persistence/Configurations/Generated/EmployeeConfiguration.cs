using MetaForge.Hrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Hrm.Infrastructure.Persistence.Configurations.Generated;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", "hrm");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeNo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DepartmentId).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
