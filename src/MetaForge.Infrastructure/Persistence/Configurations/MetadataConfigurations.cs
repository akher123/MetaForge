using MetaForge.Domain.Audit;
using MetaForge.Domain.Security;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaForge.Infrastructure.Persistence.Configurations;

public class ForgeFormConfiguration : IEntityTypeConfiguration<ForgeForm>
{
    public void Configure(EntityTypeBuilder<ForgeForm> builder)
    {
        builder.ToTable("ForgeForms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TableName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FormType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.Fields).WithOne(x => x.Form).HasForeignKey(x => x.FormId);
        builder.HasMany(x => x.Relations).WithOne(x => x.Form).HasForeignKey(x => x.FormId);
        builder.HasMany(x => x.GridColumns).WithOne(x => x.Form).HasForeignKey(x => x.FormId);
        builder.HasMany(x => x.GridActions).WithOne(x => x.Form).HasForeignKey(x => x.FormId);
    }
}

public class ForgeFieldConfiguration : IEntityTypeConfiguration<ForgeField>
{
    public void Configure(EntityTypeBuilder<ForgeField> builder)
    {
        builder.ToTable("ForgeFields");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ControlType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LookupParentField).HasMaxLength(200);
        builder.Property(x => x.LookupFilterField).HasMaxLength(200);
        builder.Property(x => x.MappingEntity).HasMaxLength(200);
        builder.Property(x => x.MappingParentKey).HasMaxLength(200);
        builder.Property(x => x.MappingRelatedKey).HasMaxLength(200);
    }
}

public class ForgeRelationConfiguration : IEntityTypeConfiguration<ForgeRelation>
{
    public void Configure(EntityTypeBuilder<ForgeRelation> builder)
    {
        builder.ToTable("ForgeRelations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ParentEntity).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ChildEntity).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ForeignKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TabLabel).HasMaxLength(200);
    }
}

public class ForgeGridColumnConfiguration : IEntityTypeConfiguration<ForgeGridColumn>
{
    public void Configure(EntityTypeBuilder<ForgeGridColumn> builder)
    {
        builder.ToTable("ForgeGridColumns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayFormat).HasMaxLength(100);
    }
}

public class ForgeReportConfiguration : IEntityTypeConfiguration<ForgeReport>
{
    public void Configure(EntityTypeBuilder<ForgeReport> builder)
    {
        builder.ToTable("ForgeReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GroupName).HasMaxLength(100);
        builder.Property(x => x.ReportType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ExportTitle).HasMaxLength(200);
        builder.Property(x => x.ShowTitleUnderline).HasDefaultValue(true);
        builder.Property(x => x.ShowSignatureBlock).HasDefaultValue(false);
        builder.Property(x => x.HeaderLeft).HasMaxLength(500);
        builder.Property(x => x.HeaderCenter).HasMaxLength(500);
        builder.Property(x => x.HeaderRight).HasMaxLength(500);
        builder.Property(x => x.FooterLeft).HasMaxLength(500);
        builder.Property(x => x.FooterCenter).HasMaxLength(500);
        builder.Property(x => x.FooterRight).HasMaxLength(500);
        builder.Property(x => x.ShowPageNumbers).HasDefaultValue(true);
        builder.Property(x => x.ShowGeneratedTimestamp).HasDefaultValue(true);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.Columns).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
        builder.HasMany(x => x.Filters).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
        builder.HasMany(x => x.Groups).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
        builder.HasMany(x => x.Summaries).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
        builder.HasMany(x => x.Signatures).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
    }
}

public class ForgeReportSignatureConfiguration : IEntityTypeConfiguration<ForgeReportSignature>
{
    public void Configure(EntityTypeBuilder<ForgeReportSignature> builder)
    {
        builder.ToTable("ForgeReportSignatures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
    }
}

public class ForgeReportColumnConfiguration : IEntityTypeConfiguration<ForgeReportColumn>
{
    public void Configure(EntityTypeBuilder<ForgeReportColumn> builder)
    {
        builder.ToTable("ForgeReportColumns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ColumnRole).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.AggregateFunction).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.DisplayFormat).HasMaxLength(100);
        builder.Property(x => x.Formula).HasMaxLength(500);
    }
}

public class ForgeReportFilterConfiguration : IEntityTypeConfiguration<ForgeReportFilter>
{
    public void Configure(EntityTypeBuilder<ForgeReportFilter> builder)
    {
        builder.ToTable("ForgeReportFilters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Operator).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ControlType).HasConversion<string>().HasMaxLength(50).IsRequired().HasDefaultValue(ReportFilterControlType.TextBox);
        builder.Property(x => x.LookupEntity).HasMaxLength(200);
        builder.Property(x => x.Options).HasMaxLength(1000);
        builder.Property(x => x.DefaultValue).HasMaxLength(500);
    }
}

public class ForgeReportGroupConfiguration : IEntityTypeConfiguration<ForgeReportGroup>
{
    public void Configure(EntityTypeBuilder<ForgeReportGroup> builder)
    {
        builder.ToTable("ForgeReportGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
    }
}

public class ForgeReportSummaryConfiguration : IEntityTypeConfiguration<ForgeReportSummary>
{
    public void Configure(EntityTypeBuilder<ForgeReportSummary> builder)
    {
        builder.ToTable("ForgeReportSummaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AggregateFunction).HasConversion<string>().HasMaxLength(50).IsRequired();
    }
}

public class ForgeFormActionConfiguration : IEntityTypeConfiguration<ForgeFormAction>
{
    public void Configure(EntityTypeBuilder<ForgeFormAction> builder)
    {
        builder.ToTable("ForgeFormActions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.Placement).HasMaxLength(50).IsRequired();
        builder.Property(x => x.HandlerType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.HandlerTarget).HasMaxLength(500).IsRequired();
        builder.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
        builder.Property(x => x.RequestBody).HasMaxLength(2000);
        builder.Property(x => x.PermissionAction).HasMaxLength(50);
        builder.Property(x => x.ConfirmMessage).HasMaxLength(500);
        builder.Property(x => x.ButtonStyle).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.FormId, x.Code }).IsUnique();
    }
}

public class ForgeMenuConfiguration : IEntityTypeConfiguration<ForgeMenu>
{
    public void Configure(EntityTypeBuilder<ForgeMenu> builder)
    {
        builder.ToTable("ForgeMenus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.ItemType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50);
        builder.Property(x => x.Url).HasMaxLength(500);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Form)
            .WithMany()
            .HasForeignKey(x => x.FormId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ParentId);
        builder.HasIndex(x => x.FormId);
    }
}

public class LookupConfigurationEntityConfiguration : IEntityTypeConfiguration<LookupConfiguration>
{
    public void Configure(EntityTypeBuilder<LookupConfiguration> builder)
    {
        builder.ToTable("LookupConfigurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.EntityName).IsUnique();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SecurityStamp).HasMaxLength(64);
        builder.Property(x => x.ThemeKey).HasMaxLength(50).IsRequired().HasDefaultValue("indigo-light");
        builder.HasIndex(x => x.UserName).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => new { x.UserId, x.RoleId });
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecordId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.EntityName, x.RecordId });
    }
}
