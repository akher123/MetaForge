using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddForgeGridColumnDisplayFormat : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeGridColumns', 'DisplayFormat') IS NULL
            BEGIN
                ALTER TABLE [ForgeGridColumns] ADD [DisplayFormat] nvarchar(100) NULL;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeGridColumns', 'DisplayFormat') IS NOT NULL
            BEGIN
                ALTER TABLE [ForgeGridColumns] DROP COLUMN [DisplayFormat];
            END
            """);
    }
}
