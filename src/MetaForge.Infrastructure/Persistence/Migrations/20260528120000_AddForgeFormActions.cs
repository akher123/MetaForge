using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddForgeFormActions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ForgeFormActions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FormId = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Placement = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                HandlerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                HandlerTarget = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                RequestBody = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PermissionAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ConfirmMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ButtonStyle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgeFormActions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgeFormActions_ForgeForms_FormId",
                    column: x => x.FormId,
                    principalTable: "ForgeForms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ForgeFormActions_FormId_Code",
            table: "ForgeFormActions",
            columns: new[] { "FormId", "Code" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForgeFormActions");
    }
}
