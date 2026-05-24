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
