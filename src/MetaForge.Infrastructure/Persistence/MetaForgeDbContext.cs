using MetaForge.Domain.Audit;
using MetaForge.Domain.Features;
using MetaForge.Domain.Security;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core database context.
/// </summary>
public class MetaForgeDbContext : DbContext
{
    public MetaForgeDbContext(DbContextOptions<MetaForgeDbContext> options) : base(options) { }

    public DbSet<ForgeForm> ForgeForms => Set<ForgeForm>();
    public DbSet<ForgeField> ForgeFields => Set<ForgeField>();
    public DbSet<ForgeRelation> ForgeRelations => Set<ForgeRelation>();
    public DbSet<ForgeGridColumn> ForgeGridColumns => Set<ForgeGridColumn>();
    public DbSet<ForgeFormAction> ForgeFormActions => Set<ForgeFormAction>();
    public DbSet<ForgeReport> ForgeReports => Set<ForgeReport>();
    public DbSet<ForgeReportColumn> ForgeReportColumns => Set<ForgeReportColumn>();
    public DbSet<ForgeReportFilter> ForgeReportFilters => Set<ForgeReportFilter>();
    public DbSet<ForgeReportGroup> ForgeReportGroups => Set<ForgeReportGroup>();
    public DbSet<ForgeReportSummary> ForgeReportSummaries => Set<ForgeReportSummary>();
    public DbSet<ForgeReportSignature> ForgeReportSignatures => Set<ForgeReportSignature>();
    public DbSet<ForgeMenu> ForgeMenus => Set<ForgeMenu>();
    public DbSet<LookupConfiguration> LookupConfigurations => Set<LookupConfiguration>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerRegion> CustomerRegions => Set<CustomerRegion>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<SalesOrderCharge> SalesOrderCharges => Set<SalesOrderCharge>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Semester> Semesters => Set<Semester>();

    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetaForgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
