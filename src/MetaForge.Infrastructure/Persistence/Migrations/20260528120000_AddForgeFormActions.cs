using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddForgeFormActions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ForgeFormActions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ForgeFormActions] (
                    [Id] int NOT NULL IDENTITY,
                    [FormId] int NOT NULL,
                    [Code] nvarchar(100) NOT NULL,
                    [Label] nvarchar(200) NOT NULL,
                    [Icon] nvarchar(100) NULL,
                    [Placement] nvarchar(50) NOT NULL,
                    [HandlerType] nvarchar(50) NOT NULL,
                    [HandlerTarget] nvarchar(500) NOT NULL,
                    [HttpMethod] nvarchar(10) NOT NULL,
                    [RequestBody] nvarchar(2000) NULL,
                    [PermissionAction] nvarchar(50) NULL,
                    [ConfirmMessage] nvarchar(500) NULL,
                    [ButtonStyle] nvarchar(50) NOT NULL,
                    [DisplayOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_ForgeFormActions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ForgeFormActions_ForgeForms_FormId] FOREIGN KEY ([FormId]) REFERENCES [ForgeForms] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_ForgeFormActions_FormId_Code] ON [ForgeFormActions] ([FormId], [Code]);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForgeFormActions");
    }
}
