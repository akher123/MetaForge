using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FromDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUseSsl = table.Column<bool>(type: "bit", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CredentialSecretName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxDegreeOfParallelism = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailRetryPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    BackoffStrategy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    MaxDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    BackoffMultiplier = table.Column<double>(type: "float", nullable: false),
                    UseJitter = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRetryPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultToExpression = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultCc = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultBcc = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmailChannelId = table.Column<int>(type: "int", nullable: true),
                    RetryPolicyId = table.Column<int>(type: "int", nullable: true),
                    Culture = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_EmailChannels_EmailChannelId",
                        column: x => x.EmailChannelId,
                        principalTable: "EmailChannels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailTemplates_EmailRetryPolicies_RetryPolicyId",
                        column: x => x.RetryPolicyId,
                        principalTable: "EmailRetryPolicies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailTemplateId = table.Column<int>(type: "int", nullable: true),
                    EmailChannelId = table.Column<int>(type: "int", nullable: false),
                    RetryPolicyId = table.Column<int>(type: "int", nullable: false),
                    ToAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Cc = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Bcc = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceRecordId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessages_EmailChannels_EmailChannelId",
                        column: x => x.EmailChannelId,
                        principalTable: "EmailChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailMessages_EmailRetryPolicies_RetryPolicyId",
                        column: x => x.RetryPolicyId,
                        principalTable: "EmailRetryPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailMessages_EmailTemplates_EmailTemplateId",
                        column: x => x.EmailTemplateId,
                        principalTable: "EmailTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplateBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailTemplateId = table.Column<int>(type: "int", nullable: false),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    TriggerEvent = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientField = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConditionExpression = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplateBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplateBindings_EmailTemplates_EmailTemplateId",
                        column: x => x.EmailTemplateId,
                        principalTable: "EmailTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailTemplateBindings_ForgeForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ForgeForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailChannels_Code",
                table: "EmailChannels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_CreatedUtc",
                table: "EmailMessages",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_EmailChannelId",
                table: "EmailMessages",
                column: "EmailChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_EmailTemplateId",
                table: "EmailMessages",
                column: "EmailTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_RetryPolicyId",
                table: "EmailMessages",
                column: "RetryPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_Status_NextAttemptUtc",
                table: "EmailMessages",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRetryPolicies_Code",
                table: "EmailRetryPolicies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateBindings_EmailTemplateId",
                table: "EmailTemplateBindings",
                column: "EmailTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateBindings_FormId",
                table: "EmailTemplateBindings",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Code",
                table: "EmailTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_EmailChannelId",
                table: "EmailTemplates",
                column: "EmailChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_RetryPolicyId",
                table: "EmailTemplates",
                column: "RetryPolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailTemplateBindings");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "EmailChannels");

            migrationBuilder.DropTable(
                name: "EmailRetryPolicies");
        }
    }
}
