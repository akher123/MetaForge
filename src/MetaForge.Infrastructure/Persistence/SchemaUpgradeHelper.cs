using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Idempotent schema patches for migrations that may not have been applied yet.
/// </summary>
internal static class SchemaUpgradeHelper
{
    public static async Task EnsureForgeFormActionsTableAsync(
        MetaForgeDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'[ForgeFormActions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeFormActions] (
                    [Id] int NOT NULL IDENTITY,
                    [FormId] int NOT NULL,
                    [Code] nvarchar(100) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [Icon] nvarchar(100) NULL,
                    [Placement] nvarchar(50) NOT NULL,
                    [HandlerType] nvarchar(50) NOT NULL,
                    [HandlerTarget] nvarchar(500) NOT NULL,
                    [HttpMethod] nvarchar(10) NOT NULL,
                    [RequestBody] nvarchar(2000) NULL,
                    [PermissionAction] nvarchar(50) NULL,
                    [ConfirmMessage] nvarchar(500) NULL,
                    [ButtonStyle] nvarchar(50) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_ForgeFormActions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeFormActions_ForgeForms_FormId] FOREIGN KEY ([FormId]) REFERENCES [ForgeForms] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_ForgeFormActions_FormId_Code] ON [ForgeFormActions] ([FormId], [Code]);
            END
            """;

        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        logger.LogInformation("Ensured ForgeFormActions table exists.");
    }

    public static async Task EnsureForgeFieldConditionalRuleColumnAsync(
        MetaForgeDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF COL_LENGTH('ForgeFields', 'ConditionalRule') IS NULL
            BEGIN
                ALTER TABLE [ForgeFields] ADD [ConditionalRule] nvarchar(max) NULL;
            END
            """;

        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        logger.LogInformation("Ensured ForgeFields.ConditionalRule column exists.");
    }
}
