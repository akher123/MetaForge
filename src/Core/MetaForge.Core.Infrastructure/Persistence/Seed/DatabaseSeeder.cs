using MetaForge.Application.Interfaces;
using MetaForge.Domain.Enums;
using MetaForge.Domain.Notifications;
using MetaForge.Domain.Security;
using MetaForge.Infrastructure.Services;
using MetaForge.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds platform security, permissions, and framework defaults.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task ResetAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetaForgeDbContext>>();

        logger.LogWarning("Dropping database...");
        await context.Database.EnsureDeletedAsync();
        logger.LogInformation("Database dropped. Applying migrations and seeding...");
        await DatabaseMigrator.MigrateAsync(context, logger);
        await SeedDataAsync(scope, context, logger);
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetaForgeDbContext>>();

        await DatabaseMigrator.MigrateAsync(context, logger);

        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Database already seeded.");
            await ApplyPlatformUpgradesAsync(context, scope, logger);
            return;
        }

        await SeedDataAsync(scope, context, logger);
    }

    private static async Task SeedDataAsync(IServiceScope scope, MetaForgeDbContext context, ILogger logger)
    {
        SeedPlatformSecurity(context);
        await context.SaveChangesAsync();
        await ApplyPlatformUpgradesAsync(context, scope, logger);
        logger.LogInformation("Database seeded successfully.");
    }

    private static async Task ApplyPlatformUpgradesAsync(
        MetaForgeDbContext context,
        IServiceScope scope,
        ILogger logger)
    {
        await EnsureUserSecurityStampsAsync(context, logger);
        await UpgradeLegacyPasswordsAsync(context, logger);
        await EnsureSecurityPermissionsAsync(context, logger);
        await EnsureFormPermissionsAsync(context, logger);
        await EnsureEmailDefaultsAsync(context, logger);
        await EnsurePasswordResetEmailTemplateAsync(context, logger);
        await EnsureEmailPermissionsAsync(context, logger);
        await EnsureReportPermissionsAsync(context, logger);
        await SystemSettingsSeed.EnsureDefaultsAsync(context, logger);
        await SystemSettingsSeed.EnsurePermissionsAsync(context, logger);
        await EnsureMenusAsync(scope, logger);
    }

    private static async Task EnsureReportPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var reports = await context.ForgeReports.Where(r => r.IsActive).AsNoTracking().ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var (code, name, action) in Shared.Constants.ReportConfigPermissions.All)
        {
            if (!permissionByCode.TryGetValue(code, out var permission))
            {
                permission = new Permission { FormId = 0, Action = action, Code = code, Name = name };
                context.Permissions.Add(permission);
                permissionByCode[code] = permission;
                addedPermissions++;
            }

            if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                continue;

            var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
            if (alreadyAssigned)
            {
                adminPermissionIds.Add(permission.Id);
                continue;
            }

            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
            if (permission.Id > 0)
                adminPermissionIds.Add(permission.Id);
            addedAssignments++;
        }

        foreach (var report in reports)
        {
            foreach (var action in Shared.Constants.ReportPermissionAction.All)
            {
                var code = $"{report.Code}.{action}";
                if (!permissionByCode.TryGetValue(code, out var permission))
                {
                    permission = new Permission
                    {
                        FormId = 0,
                        Action = action,
                        Code = code,
                        Name = $"{report.Name} - {action}"
                    };
                    context.Permissions.Add(permission);
                    permissionByCode[code] = permission;
                    addedPermissions++;
                }

                if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                    continue;

                var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                    rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
                if (alreadyAssigned)
                {
                    adminPermissionIds.Add(permission.Id);
                    continue;
                }

                context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
                if (permission.Id > 0)
                    adminPermissionIds.Add(permission.Id);
                addedAssignments++;
            }
        }

        if (addedPermissions > 0 || addedAssignments > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Synced report permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static async Task EnsureEmailDefaultsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var added = 0;

        if (!await context.EmailRetryPolicies.AnyAsync(p => p.Code == "standard"))
        {
            context.EmailRetryPolicies.Add(new EmailRetryPolicy
            {
                Code = "standard",
                Name = "Standard Retry",
                MaxAttempts = 5,
                BackoffStrategy = EmailBackoffStrategy.Exponential,
                BaseDelaySeconds = 60,
                MaxDelaySeconds = 3600,
                BackoffMultiplier = 2.0,
                UseJitter = true,
                IsActive = true,
                IsDefault = true
            });
            added++;
        }

        if (!await context.EmailChannels.AnyAsync(c => c.Code == "default-smtp"))
        {
            context.EmailChannels.Add(new EmailChannel
            {
                Code = "default-smtp",
                Name = "Default SMTP",
                Provider = EmailProviderType.Smtp,
                FromAddress = "noreply@example.com",
                FromDisplayName = "MetaForge",
                SmtpHost = "localhost",
                SmtpPort = 587,
                SmtpUseSsl = true,
                CredentialSecretName = "default-smtp",
                IsActive = true,
                IsDefault = true
            });
            added++;
        }

        if (!await context.EmailChannels.AnyAsync(c => c.Code == "sendgrid"))
        {
            context.EmailChannels.Add(new EmailChannel
            {
                Code = "sendgrid",
                Name = "SendGrid",
                Provider = EmailProviderType.SendGrid,
                FromAddress = "noreply@example.com",
                FromDisplayName = "MetaForge",
                CredentialSecretName = "sendgrid-main",
                IsActive = false,
                IsDefault = false
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} default email configuration record(s).", added);
        }
    }

    private static async Task EnsurePasswordResetEmailTemplateAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (await context.EmailTemplates.AnyAsync(t => t.Code == "password-reset"))
            return;

        var channel = await context.EmailChannels.FirstOrDefaultAsync(c => c.IsDefault && c.IsActive)
            ?? await context.EmailChannels.FirstOrDefaultAsync(c => c.IsActive);
        var policy = await context.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive)
            ?? await context.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsActive);

        context.EmailTemplates.Add(new EmailTemplate
        {
            Code = "password-reset",
            Name = "Password Reset",
            Description = "Sent when a user must set or reset their password.",
            Subject = "Set your {{AppName}} password",
            DefaultToExpression = "{{Email}}",
            BodyHtml = """
                <p>Hello {{UserName}},</p>
                <p>Use the link below to set your password. This link expires in {{ExpiresHours}} hour(s).</p>
                <p><a href="{{ResetLink}}">Set my password</a></p>
                <p>If you did not request this, you can ignore this email.</p>
                """,
            BodyText = """
                Hello {{UserName}},

                Use the link below to set your password. This link expires in {{ExpiresHours}} hour(s).

                {{ResetLink}}

                If you did not request this, you can ignore this email.
                """,
            EmailChannelId = channel?.Id,
            RetryPolicyId = policy?.Id,
            IsActive = true
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded password reset email template.");
    }

    private static async Task EnsureEmailPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var (code, name, action) in Shared.Constants.EmailConfigPermissions.All)
        {
            if (!permissionByCode.TryGetValue(code, out var permission))
            {
                permission = new Permission { FormId = 0, Action = action, Code = code, Name = name };
                context.Permissions.Add(permission);
                permissionByCode[code] = permission;
                addedPermissions++;
            }

            if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                continue;

            var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
            if (alreadyAssigned)
            {
                adminPermissionIds.Add(permission.Id);
                continue;
            }

            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
            if (permission.Id > 0)
                adminPermissionIds.Add(permission.Id);
            addedAssignments++;
        }

        if (addedPermissions > 0 || addedAssignments > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Synced email permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static async Task EnsureMenusAsync(IServiceScope scope, ILogger logger)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();

            await context.ForgeMenus
                .Where(m => m.Action == "MasterDetail")
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Action, "Index"));

            var menuSync = scope.ServiceProvider.GetRequiredService<IMenuSyncService>();
            await menuSync.EnsureDefaultMenusAsync();
            await menuSync.EnsureSystemAdminMenusAsync();
            await menuSync.EnsureAccountMenusAsync();
            logger.LogInformation("Navigation menus ensured.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure navigation menus.");
        }
    }

    private static async Task EnsureUserSecurityStampsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var users = await context.Users
            .Where(u => u.SecurityStamp == null || u.SecurityStamp == string.Empty)
            .ToListAsync();

        if (users.Count == 0)
            return;

        foreach (var user in users)
            user.SecurityStamp = Guid.NewGuid().ToString("N");

        await context.SaveChangesAsync();
        logger.LogInformation("Assigned security stamps to {Count} user(s).", users.Count);
    }

    private static async Task EnsureSecurityPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var existing = await context.Permissions.Select(p => p.Code).ToListAsync();
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (code, name, action) in Shared.Constants.SecurityPermissions.All)
        {
            if (set.Contains(code)) continue;
            var perm = new Permission { Action = action, Code = code, Name = name };
            context.Permissions.Add(perm);
            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = perm });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} security permissions.", added);
        }
    }

    private static async Task EnsureFormPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var modules = await context.ForgeForms.Where(m => m.IsActive).AsNoTracking().ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var module in modules)
        {
            foreach (var action in PermissionAction.All)
            {
                var code = $"{module.Code}.{action}";
                if (!permissionByCode.TryGetValue(code, out var permission))
                {
                    permission = new Permission
                    {
                        FormId = module.Id,
                        Action = action,
                        Code = code,
                        Name = $"{module.Name} - {action}"
                    };
                    context.Permissions.Add(permission);
                    permissionByCode[code] = permission;
                    addedPermissions++;
                }

                if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                    continue;

                if (permission.Id > 0)
                {
                    var alreadyAssigned = await context.RolePermissions.AnyAsync(
                        rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
                    if (alreadyAssigned)
                    {
                        adminPermissionIds.Add(permission.Id);
                        continue;
                    }
                }

                context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
                if (permission.Id > 0)
                    adminPermissionIds.Add(permission.Id);
                addedAssignments++;
            }
        }

        if (addedPermissions > 0 || addedAssignments > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Synced module permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static async Task UpgradeLegacyPasswordsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminPasswordHash = await context.Users
            .AsNoTracking()
            .Where(u => u.UserName == "admin")
            .Select(u => u.PasswordHash)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(adminPasswordHash) || !PasswordHasher.IsLegacyHash(adminPasswordHash))
            return;

        var admin = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");
        if (admin == null)
            return;

        admin.PasswordHash = PasswordHasher.Hash("admin");
        await context.SaveChangesAsync();
        logger.LogInformation("Upgraded legacy admin password hash.");
    }

    private static void SeedPlatformSecurity(MetaForgeDbContext context)
    {
        var adminRole = new Role { Name = "Administrator", Description = "Full access" };
        context.Roles.Add(adminRole);

        context.Users.Add(new User
        {
            UserName = "admin",
            Email = "admin@localhost",
            PasswordHash = PasswordHasher.Hash("admin"),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            UserRoles = [new UserRole { Role = adminRole }]
        });

        foreach (var (code, name, action) in Shared.Constants.SecurityPermissions.All)
        {
            context.Permissions.Add(new Permission
            {
                Action = action,
                Code = code,
                Name = name,
                RolePermissions = [new RolePermission { Role = adminRole }]
            });
        }

        foreach (var (code, name, action) in Shared.Constants.ConfigPermissions.All)
        {
            context.Permissions.Add(new Permission
            {
                Action = action,
                Code = code,
                Name = name,
                RolePermissions = [new RolePermission { Role = adminRole }]
            });
        }
    }
}
