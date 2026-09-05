
using MetaForge.Infrastructure.Persistence;
using MetaForge.Modules.Abstractions;
using Microsoft.EntityFrameworkCore;

using MetaForge.Hrm.Infrastructure;
namespace MetaForge.Web.Modules;

/// <summary>
/// Registers enabled business modules and applies their EF migrations at startup.
/// </summary>
public static class MetaForgeModuleRegistration
{
    public static IServiceCollection AddMetaForgeModules(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddHrmModule(configuration);
        services.AddScoped<IModuleDbContextResolver, ModuleDbContextResolver>();
        return services;
    }

    public static async Task MigrateAllModulesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MetaForge.Migrations");

        var core = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        await DatabaseMigrator.MigrateAsync(core, logger, cancellationToken);

        foreach (var module in scope.ServiceProvider.GetServices<IMetaForgeModule>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType);
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count == 0)
                continue;

            logger.LogInformation("Applying {Count} migration(s) for module {Module}: {Migrations}",
                pending.Count, module.Name, string.Join(", ", pending));
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
