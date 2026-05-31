using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddForgeReports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ForgeReports]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReports] (
                    [Id] int NOT NULL IDENTITY,
                    [Code] nvarchar(100) NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [EntityName] nvarchar(200) NOT NULL,
                    [GroupName] nvarchar(100) NULL,
                    [ReportType] nvarchar(50) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [Description] nvarchar(500) NULL,
                    CONSTRAINT [PK_ForgeReports] PRIMARY KEY ([Id])
                );

                CREATE UNIQUE INDEX [IX_ForgeReports_Code] ON [ForgeReports] ([Code]);
            END

            IF OBJECT_ID(N'[ForgeReportColumns]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReportColumns] (
                    [Id] int NOT NULL IDENTITY,
                    [ReportId] int NOT NULL,
                    [PropertyName] nvarchar(200) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    [IsVisible] bit NOT NULL,
                    [ColumnRole] nvarchar(50) NOT NULL,
                    [AggregateFunction] nvarchar(50) NOT NULL,
                    [DisplayFormat] nvarchar(100) NULL,
                    [Formula] nvarchar(500) NULL,
                    CONSTRAINT [PK_ForgeReportColumns] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeReportColumns_ForgeReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [ForgeReports] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[ForgeReportFilters]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReportFilters] (
                    [Id] int NOT NULL IDENTITY,
                    [ReportId] int NOT NULL,
                    [PropertyName] nvarchar(200) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [Operator] nvarchar(50) NOT NULL,
                    [DefaultValue] nvarchar(500) NULL,
                    [IsRequired] bit NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    CONSTRAINT [PK_ForgeReportFilters] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeReportFilters_ForgeReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [ForgeReports] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[ForgeReportGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReportGroups] (
                    [Id] int NOT NULL IDENTITY,
                    [ReportId] int NOT NULL,
                    [PropertyName] nvarchar(200) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    [SortDescending] bit NOT NULL,
                    [ShowSubtotal] bit NOT NULL,
                    [ShowGroupHeader] bit NOT NULL,
                    CONSTRAINT [PK_ForgeReportGroups] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeReportGroups_ForgeReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [ForgeReports] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[ForgeReportSummaries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeReportSummaries] (
                    [Id] int NOT NULL IDENTITY,
                    [ReportId] int NOT NULL,
                    [PropertyName] nvarchar(200) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [AggregateFunction] nvarchar(50) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    CONSTRAINT [PK_ForgeReportSummaries] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeReportSummaries_ForgeReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [ForgeReports] ([Id]) ON DELETE CASCADE
                );
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ForgeReportSummaries");
        migrationBuilder.DropTable(name: "ForgeReportGroups");
        migrationBuilder.DropTable(name: "ForgeReportFilters");
        migrationBuilder.DropTable(name: "ForgeReportColumns");
        migrationBuilder.DropTable(name: "ForgeReports");
    }
}
