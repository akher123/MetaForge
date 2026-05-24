using System.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Creates missing business tables on an existing database without a full reset.
/// </summary>
internal static class BusinessTableEnsurer
{
    private const string BusinessNamespace = "MetaForge.Domain.Business";

    public static async Task EnsureMissingTablesAsync(
        MetaForgeDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var businessEntities = context.Model.GetEntityTypes()
            .Where(t => t.ClrType.Namespace?.StartsWith(BusinessNamespace, StringComparison.Ordinal) == true)
            .ToList();

        if (businessEntities.Count == 0)
            return;

        var missingTables = new List<string>();
        foreach (var entity in businessEntities)
        {
            var tableName = entity.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            if (!await TableExistsAsync(context, tableName, cancellationToken))
                missingTables.Add(tableName);
        }

        if (missingTables.Count == 0)
            return;

        var infrastructure = context.GetInfrastructure();
        var differ = infrastructure.GetRequiredService<IMigrationsModelDiffer>();
        var sqlGenerator = infrastructure.GetRequiredService<IMigrationsSqlGenerator>();
        var sourceModel = CreateEmptyRelationalModel(context);
        var targetModel = context.Model.GetRelationalModel();

        var operations = differ.GetDifferences(sourceModel, targetModel)
            .Where(op => AffectsMissingTable(op, missingTables))
            .ToList();

        foreach (var operation in operations)
        {
            var tableName = GetTableName(operation);
            logger.LogInformation("Creating missing business table {TableName}", tableName ?? operation.GetType().Name);

            foreach (var command in sqlGenerator.Generate([operation]))
                await context.Database.ExecuteSqlRawAsync(command.CommandText, cancellationToken);
        }
    }

    private static IRelationalModel CreateEmptyRelationalModel(MetaForgeDbContext context)
    {
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is required.");

        var options = new DbContextOptionsBuilder<EmptySchemaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var emptyContext = new EmptySchemaDbContext(options);
        return emptyContext.Model.GetRelationalModel();
    }

    private static bool AffectsMissingTable(MigrationOperation operation, IReadOnlyCollection<string> missingTables) =>
        operation switch
        {
            CreateTableOperation create => missingTables.Contains(create.Name, StringComparer.OrdinalIgnoreCase),
            CreateIndexOperation index => missingTables.Contains(index.Table, StringComparer.OrdinalIgnoreCase),
            AddForeignKeyOperation fk => missingTables.Contains(fk.Table, StringComparer.OrdinalIgnoreCase),
            _ => false
        };

    private static string? GetTableName(MigrationOperation operation) =>
        operation switch
        {
            CreateTableOperation create => create.Name,
            CreateIndexOperation index => index.Table,
            AddForeignKeyOperation fk => fk.Table,
            _ => null
        };

    private static async Task<bool> TableExistsAsync(
        MetaForgeDbContext context,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            openedHere = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = @tableName) THEN 1 ELSE 0 END";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private sealed class EmptySchemaDbContext(DbContextOptions options) : DbContext(options);
}
