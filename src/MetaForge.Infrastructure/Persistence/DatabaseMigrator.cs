using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations at application startup.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(MetaForgeDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingList = pending.ToList();

        if (pendingList.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
            pendingList.Count,
            string.Join(", ", pendingList));

        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migration completed.");
    }
}
