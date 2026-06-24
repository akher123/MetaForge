using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMultiSelectMappingSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('ForgeFields', 'MappingEntity') IS NULL
                ALTER TABLE [ForgeFields] ADD [MappingEntity] nvarchar(200) NULL;

            IF COL_LENGTH('ForgeFields', 'MappingParentKey') IS NULL
                ALTER TABLE [ForgeFields] ADD [MappingParentKey] nvarchar(200) NULL;

            IF COL_LENGTH('ForgeFields', 'MappingRelatedKey') IS NULL
                ALTER TABLE [ForgeFields] ADD [MappingRelatedKey] nvarchar(200) NULL;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[CustomerRegions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CustomerRegions] (
                    [CustomerId] int NOT NULL,
                    [RegionId] int NOT NULL,
                    CONSTRAINT [PK_CustomerRegions] PRIMARY KEY ([CustomerId], [RegionId]),
                    CONSTRAINT [FK_CustomerRegions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CustomerRegions_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE NO ACTION
                );

                CREATE INDEX [IX_CustomerRegions_RegionId] ON [CustomerRegions] ([RegionId]);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[CustomerRegions]', N'U') IS NOT NULL
                DROP TABLE [CustomerRegions];

            IF COL_LENGTH('ForgeFields', 'MappingRelatedKey') IS NOT NULL
                ALTER TABLE [ForgeFields] DROP COLUMN [MappingRelatedKey];

            IF COL_LENGTH('ForgeFields', 'MappingParentKey') IS NOT NULL
                ALTER TABLE [ForgeFields] DROP COLUMN [MappingParentKey];

            IF COL_LENGTH('ForgeFields', 'MappingEntity') IS NOT NULL
                ALTER TABLE [ForgeFields] DROP COLUMN [MappingEntity];
            """);
    }
}
