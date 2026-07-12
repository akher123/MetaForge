using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMultiTableTreeSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cities",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RegionId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cities", x => x.Id);
                table.ForeignKey(
                    name: "FK_Cities_Regions_RegionId",
                    column: x => x.RegionId,
                    principalTable: "Regions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ForgeTreeLevels",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FormId = table.Column<int>(type: "int", nullable: false),
                LevelIndex = table.Column<int>(type: "int", nullable: false),
                EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ParentEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ForeignKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                DisplayColumn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgeTreeLevels", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgeTreeLevels_ForgeForms_FormId",
                    column: x => x.FormId,
                    principalTable: "ForgeForms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Cities_RegionId",
            table: "Cities",
            column: "RegionId");

        migrationBuilder.CreateIndex(
            name: "IX_ForgeTreeLevels_FormId_LevelIndex",
            table: "ForgeTreeLevels",
            columns: new[] { "FormId", "LevelIndex" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Cities");
        migrationBuilder.DropTable(name: "ForgeTreeLevels");
    }
}
