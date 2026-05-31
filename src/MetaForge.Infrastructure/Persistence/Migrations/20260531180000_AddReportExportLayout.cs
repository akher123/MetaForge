using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportExportLayout : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ForgeReports', 'ExportTitle') IS NULL
                ALTER TABLE [ForgeReports] ADD [ExportTitle] nvarchar(200) NULL;

            IF COL_LENGTH('ForgeReports', 'ShowTitleUnderline') IS NULL
                ALTER TABLE [ForgeReports] ADD [ShowTitleUnderline] bit NOT NULL CONSTRAINT [DF_ForgeReports_ShowTitleUnderline] DEFAULT 1;

            IF COL_LENGTH('ForgeReports', 'ShowSignatureBlock') IS NULL
                ALTER TABLE [ForgeReports] ADD [ShowSignatureBlock] bit NOT NULL CONSTRAINT [DF_ForgeReports_ShowSignatureBlock] DEFAULT 0;

            IF OBJECT_ID(N'[ForgeReportSignatures]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReportSignatures] (
                    [Id] int NOT NULL IDENTITY,
                    [ReportId] int NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    CONSTRAINT [PK_ForgeReportSignatures] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeReportSignatures_ForgeReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [ForgeReports] ([Id]) ON DELETE CASCADE
                );
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ForgeReportSignatures");

        migrationBuilder.DropColumn(name: "ExportTitle", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "ShowTitleUnderline", table: "ForgeReports");
        migrationBuilder.DropColumn(name: "ShowSignatureBlock", table: "ForgeReports");
    }
}
