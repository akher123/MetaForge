using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportFilterControls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeReportFilters', 'ControlType') IS NULL
                ALTER TABLE [ForgeReportFilters] ADD [ControlType] nvarchar(50) NOT NULL CONSTRAINT [DF_ForgeReportFilters_ControlType] DEFAULT 'TextBox';

            IF COL_LENGTH('ForgeReportFilters', 'LookupEntity') IS NULL
                ALTER TABLE [ForgeReportFilters] ADD [LookupEntity] nvarchar(200) NULL;

            IF COL_LENGTH('ForgeReportFilters', 'Options') IS NULL
                ALTER TABLE [ForgeReportFilters] ADD [Options] nvarchar(1000) NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ControlType", table: "ForgeReportFilters");
        migrationBuilder.DropColumn(name: "LookupEntity", table: "ForgeReportFilters");
        migrationBuilder.DropColumn(name: "Options", table: "ForgeReportFilters");
    }
}
