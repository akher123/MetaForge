using MetaForge.Domain.Audit;
using MetaForge.Domain.Notifications;
using MetaForge.Domain.Platform;
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
    public DbSet<ForgeTreeLevel> ForgeTreeLevels => Set<ForgeTreeLevel>();
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailChannel> EmailChannels => Set<EmailChannel>();
    public DbSet<EmailRetryPolicy> EmailRetryPolicies => Set<EmailRetryPolicy>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailTemplateBinding> EmailTemplateBindings => Set<EmailTemplateBinding>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetaForgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
