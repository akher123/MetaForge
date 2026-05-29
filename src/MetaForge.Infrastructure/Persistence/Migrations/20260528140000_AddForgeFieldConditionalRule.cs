using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddForgeFieldConditionalRule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeFields', 'ConditionalRule') IS NULL
            BEGIN
                ALTER TABLE [ForgeFields] ADD [ConditionalRule] nvarchar(max) NULL;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeFields', 'ConditionalRule') IS NOT NULL
            BEGIN
                ALTER TABLE [ForgeFields] DROP COLUMN [ConditionalRule];
            END
            """);
    }
}
