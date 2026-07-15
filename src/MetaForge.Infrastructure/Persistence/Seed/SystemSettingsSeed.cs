using MetaForge.Domain.Platform;
using MetaForge.Domain.Security;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed for platform-wide system settings.
/// </summary>
public static class SystemSettingsSeed
{
    public static async Task EnsureDefaultsAsync(MetaForgeDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var existingKeys = await context.SystemSettings
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);
        var keySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var definition in DefaultDefinitions)
        {
            if (keySet.Contains(definition.Key))
                continue;

            context.SystemSettings.Add(new SystemSetting
            {
                Key = definition.Key,
                Value = definition.Value,
                ValueType = definition.ValueType,
                Category = definition.Category,
                Description = definition.Description,
                IsEditable = true,
                UpdatedAtUtc = now
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} system setting(s).", added);
        }

        await RemoveObsoleteSettingsAsync(context, logger, cancellationToken);
    }

    public static async Task RemoveObsoleteSettingsAsync(
        MetaForgeDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string obsoleteSupportedCulturesKey = "localization.supportedCultures";
        var obsolete = await context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == obsoleteSupportedCulturesKey, cancellationToken);

        if (obsolete == null)
            return;

        context.SystemSettings.Remove(obsolete);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Removed obsolete system setting '{Key}'.", obsoleteSupportedCulturesKey);
    }

    public static async Task EnsurePermissionsAsync(MetaForgeDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator", cancellationToken);
        if (adminRole == null)
            return;

        var permissionByCode = await context.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync(cancellationToken);

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var (code, name, action) in SystemSettingsPermissions.All)
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
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id, cancellationToken);
            if (alreadyAssigned)
            {
                if (permission.Id > 0)
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
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "System settings permissions ensured ({Permissions} new, {Assignments} role assignments).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static readonly IReadOnlyList<SettingDefinition> DefaultDefinitions =
    [
        new(
            SystemSettingKeys.LocalizationEnabled,
            "true",
            SystemSettingValueTypes.Bool,
            SystemSettingCategories.Localization,
            "Enable localization for the application."),
        new(
            SystemSettingKeys.DefaultCulture,
            "en-US",
            SystemSettingValueTypes.String,
            SystemSettingCategories.Localization,
            "Default culture for users without an override."),
        new(
            SystemSettingKeys.FallbackCulture,
            "en-US",
            SystemSettingValueTypes.String,
            SystemSettingCategories.Localization,
            "Fallback culture when a translation is missing."),
        new(
            SystemSettingKeys.DefaultDateFormat,
            GridDisplayFormats.LocaleDate,
            SystemSettingValueTypes.String,
            SystemSettingCategories.Localization,
            "Default date display format for the default culture."),
        new(
            SystemSettingKeys.DefaultDateTimeFormat,
            GridDisplayFormats.LocaleDateTime,
            SystemSettingValueTypes.String,
            SystemSettingCategories.Localization,
            "Default date-time display format for the default culture."),
        new(
            SystemSettingKeys.DefaultThemeKey,
            AppThemes.Default,
            SystemSettingValueTypes.String,
            SystemSettingCategories.Appearance,
            "Default UI theme for users without an override.")
    ];

    private sealed record SettingDefinition(
        string Key,
        string Value,
        string ValueType,
        string Category,
        string Description);
}
