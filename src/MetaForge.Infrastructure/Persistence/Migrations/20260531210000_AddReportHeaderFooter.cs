using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportHeaderFooter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeReports', 'HeaderLeft') IS NULL
                ALTER TABLE [ForgeReports] ADD [HeaderLeft] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'HeaderCenter') IS NULL
                ALTER TABLE [ForgeReports] ADD [HeaderCenter] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'HeaderRight') IS NULL
                ALTER TABLE [ForgeReports] ADD [HeaderRight] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'FooterLeft') IS NULL
                ALTER TABLE [ForgeReports] ADD [FooterLeft] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'FooterCenter') IS NULL
                ALTER TABLE [ForgeReports] ADD [FooterCenter] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'FooterRight') IS NULL
                ALTER TABLE [ForgeReports] ADD [FooterRight] nvarchar(500) NULL;

            IF COL_LENGTH('ForgeReports', 'ShowPageNumbers') IS NULL
                ALTER TABLE [ForgeReports] ADD [ShowPageNumbers] bit NOT NULL CONSTRAINT [DF_ForgeReports_ShowPageNumbers] DEFAULT 1;

            IF COL_LENGTH('ForgeReports', 'ShowGeneratedTimestamp') IS NULL
                ALTER TABLE [ForgeReports] ADD [ShowGeneratedTimestamp] bit NOT NULL CONSTRAINT [DF_ForgeReports_ShowGeneratedTimestamp] DEFAULT 1;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HeaderLeft", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "HeaderCenter", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "HeaderRight", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "FooterLeft", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "FooterCenter", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "FooterRight", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "ShowPageNumbers", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "ShowGeneratedTimestamp", table: "ForgeReports");
    }
}
