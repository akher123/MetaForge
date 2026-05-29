using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserThemeKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Users', 'ThemeKey') IS NULL
            BEGIN
                ALTER TABLE [Users] ADD [ThemeKey] nvarchar(50) NOT NULL
                    CONSTRAINT [DF_Users_ThemeKey] DEFAULT 'indigo-light';
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Users', 'ThemeKey') IS NOT NULL
            BEGIN
                ALTER TABLE [Users] DROP CONSTRAINT [DF_Users_ThemeKey];
                ALTER TABLE [Users] DROP COLUMN [ThemeKey];
            END
            """);
    }
}
