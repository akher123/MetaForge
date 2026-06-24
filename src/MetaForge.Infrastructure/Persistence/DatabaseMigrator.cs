using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Applies EF Core migrations at application startup.
/// </summary>
public static class DatabaseMigrator
{
    private const string InitialMigrationId = "20260624070000_InitialCreate";
    private const string ProductVersion = "10.0.8";

    public static async Task MigrateAsync(MetaForgeDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        await EnsureLegacyBaselineAsync(context, logger, cancellationToken);

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

    /// <summary>
    /// Existing databases created before EF migrations were introduced already contain
    /// the baseline schema. Record the initial migration without re-creating tables.
    /// </summary>
    private static async Task EnsureLegacyBaselineAsync(
        MetaForgeDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
            return;

        if (!await TableExistsAsync(context, "ForgeForms", cancellationToken))
            return;

        await EnsureMigrationHistoryTableAsync(context, cancellationToken);

        if (await MigrationAppliedAsync(context, InitialMigrationId, cancellationToken))
            return;

        logger.LogWarning(
            "Existing database detected without EF migration history. Recording baseline migration '{MigrationId}' without re-creating schema.",
            InitialMigrationId);

        await InsertMigrationHistoryAsync(context, InitialMigrationId, cancellationToken);
    }

    private static async Task EnsureMigrationHistoryTableAsync(
        MetaForgeDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END
            """,
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        MetaForgeDbContext context,
        string tableName,
        CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName
                    ) THEN 1
                    ELSE 0
                END
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> MigrationAppliedAsync(
        MetaForgeDbContext context,
        string migrationId,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(context, "__EFMigrationsHistory", cancellationToken))
            return false;

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [__EFMigrationsHistory]
                        WHERE [MigrationId] = @migrationId
                    ) THEN 1
                    ELSE 0
                END
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@migrationId";
            parameter.Value = migrationId;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static Task InsertMigrationHistoryAsync(
        MetaForgeDbContext context,
        string migrationId,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {migrationId})
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES ({migrationId}, {ProductVersion});
            """,
            cancellationToken);
}
