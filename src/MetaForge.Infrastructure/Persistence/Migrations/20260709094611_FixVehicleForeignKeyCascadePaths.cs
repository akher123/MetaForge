using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixVehicleForeignKeyCascadePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InitialCreate was updated to Restrict for fresh databases; this migration only
            // upgrades older databases that still have CASCADE delete on vehicle FKs.
            EnsureRestrictForeignKey(
                migrationBuilder,
                table: "VehicleModels",
                fkName: "FK_VehicleModels_VehicleMakes_VehicleMakeId",
                column: "VehicleMakeId",
                principalTable: "VehicleMakes",
                principalColumn: "Id");

            EnsureRestrictForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleMakes_VehicleMakeId",
                column: "VehicleMakeId",
                principalTable: "VehicleMakes",
                principalColumn: "Id");

            EnsureRestrictForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleModels_VehicleModelId",
                column: "VehicleModelId",
                principalTable: "VehicleModels",
                principalColumn: "Id");

            EnsureRestrictForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleStatus_VehicleStatusId",
                column: "VehicleStatusId",
                principalTable: "VehicleStatus",
                principalColumn: "Id");

            EnsureRestrictForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleTypes_VehicleTypeId",
                column: "VehicleTypeId",
                principalTable: "VehicleTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            EnsureCascadeForeignKey(
                migrationBuilder,
                table: "VehicleModels",
                fkName: "FK_VehicleModels_VehicleMakes_VehicleMakeId",
                column: "VehicleMakeId",
                principalTable: "VehicleMakes",
                principalColumn: "Id");

            EnsureCascadeForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleMakes_VehicleMakeId",
                column: "VehicleMakeId",
                principalTable: "VehicleMakes",
                principalColumn: "Id");

            EnsureCascadeForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleModels_VehicleModelId",
                column: "VehicleModelId",
                principalTable: "VehicleModels",
                principalColumn: "Id");

            EnsureCascadeForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleStatus_VehicleStatusId",
                column: "VehicleStatusId",
                principalTable: "VehicleStatus",
                principalColumn: "Id");

            EnsureCascadeForeignKey(
                migrationBuilder,
                table: "Vehicles",
                fkName: "FK_Vehicles_VehicleTypes_VehicleTypeId",
                column: "VehicleTypeId",
                principalTable: "VehicleTypes",
                principalColumn: "Id");
        }

        private static void EnsureRestrictForeignKey(
            MigrationBuilder migrationBuilder,
            string table,
            string fkName,
            string column,
            string principalTable,
            string principalColumn)
        {
            migrationBuilder.Sql($"""
                IF OBJECT_ID(N'[{table}]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE name = N'{fkName}' AND delete_referential_action = 1)
                    BEGIN
                        ALTER TABLE [{table}] DROP CONSTRAINT [{fkName}];
                        ALTER TABLE [{table}] ADD CONSTRAINT [{fkName}]
                            FOREIGN KEY ([{column}]) REFERENCES [{principalTable}] ([{principalColumn}]);
                    END
                END
                """);
        }

        private static void EnsureCascadeForeignKey(
            MigrationBuilder migrationBuilder,
            string table,
            string fkName,
            string column,
            string principalTable,
            string principalColumn)
        {
            migrationBuilder.Sql($"""
                IF OBJECT_ID(N'[{table}]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE name = N'{fkName}' AND delete_referential_action <> 1)
                    BEGIN
                        ALTER TABLE [{table}] DROP CONSTRAINT [{fkName}];
                        ALTER TABLE [{table}] ADD CONSTRAINT [{fkName}]
                            FOREIGN KEY ([{column}]) REFERENCES [{principalTable}] ([{principalColumn}])
                            ON DELETE CASCADE;
                    END
                END
                """);
        }
    }
}
