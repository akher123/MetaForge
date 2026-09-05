using Microsoft.EntityFrameworkCore;

using MetaForge.Hrm.Domain.Entities;
namespace MetaForge.Hrm.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Hrm module (SQL Server schema: hrm).
/// </summary>
public class HrmDbContext : DbContext
{
    public HrmDbContext(DbContextOptions<HrmDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeeType> EmployeeTypes => Set<EmployeeType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("hrm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrmDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
