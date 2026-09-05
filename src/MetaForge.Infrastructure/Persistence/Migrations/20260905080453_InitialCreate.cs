using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

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
                name: "ForgeForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForgeReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReportType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExportTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShowTitleUnderline = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ShowSignatureBlock = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HeaderLeft = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeaderCenter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeaderRight = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FooterLeft = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FooterCenter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FooterRight = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShowPageNumbers = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ShowGeneratedTimestamp = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LookupConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValueField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEditable = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ThemeKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CultureOverride = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateFormatOverride = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateTimeFormatOverride = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
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
                name: "ForgeFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ControlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ValidationRule = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConditionalRule = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LookupEntity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LookupParentField = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LookupFilterField = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MappingEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MappingParentKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MappingRelatedKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SectionName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeFields_ForgeForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ForgeForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "ForgeGridColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsSortable = table.Column<bool>(type: "bit", nullable: false),
                    IsSearchable = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeGridColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeGridColumns_ForgeForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ForgeForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForgeMenus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeMenus_ForgeForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ForgeForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ForgeMenus_ForgeMenus_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ForgeMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForgeRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    RelationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParentEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChildEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ForeignKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NavigationProperty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TabLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeRelations_ForgeForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ForgeForms",
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

            migrationBuilder.CreateTable(
                name: "ForgeReportColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    ColumnRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AggregateFunction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Formula = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReportColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeReportColumns_ForgeReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ForgeReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForgeReportFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ControlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "TextBox"),
                    LookupEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Options = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReportFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeReportFilters_ForgeReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ForgeReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForgeReportGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    SortDescending = table.Column<bool>(type: "bit", nullable: false),
                    ShowSubtotal = table.Column<bool>(type: "bit", nullable: false),
                    ShowGroupHeader = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReportGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeReportGroups_ForgeReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ForgeReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForgeReportSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReportSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeReportSignatures_ForgeReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ForgeReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForgeReportSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AggregateFunction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeReportSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeReportSummaries_ForgeReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ForgeReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_AuditLogs_EntityName_RecordId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

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

            migrationBuilder.CreateIndex(
                name: "IX_ForgeFields_FormId",
                table: "ForgeFields",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeFormActions_FormId_Code",
                table: "ForgeFormActions",
                columns: new[] { "FormId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForgeForms_Code",
                table: "ForgeForms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForgeGridColumns_FormId",
                table: "ForgeGridColumns",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeMenus_FormId",
                table: "ForgeMenus",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeMenus_ParentId",
                table: "ForgeMenus",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeRelations_FormId",
                table: "ForgeRelations",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReportColumns_ReportId",
                table: "ForgeReportColumns",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReportFilters_ReportId",
                table: "ForgeReportFilters",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReportGroups_ReportId",
                table: "ForgeReportGroups",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReports_Code",
                table: "ForgeReports",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReportSignatures_ReportId",
                table: "ForgeReportSignatures",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeReportSummaries_ReportId",
                table: "ForgeReportSummaries",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgeTreeLevels_FormId_LevelIndex",
                table: "ForgeTreeLevels",
                columns: new[] { "FormId", "LevelIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LookupConfigurations_EntityName",
                table: "LookupConfigurations",
                column: "EntityName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_Purpose_UsedUtc",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "Purpose", "UsedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailTemplateBindings");

            migrationBuilder.DropTable(
                name: "ForgeFields");

            migrationBuilder.DropTable(
                name: "ForgeFormActions");

            migrationBuilder.DropTable(
                name: "ForgeGridColumns");

            migrationBuilder.DropTable(
                name: "ForgeMenus");

            migrationBuilder.DropTable(
                name: "ForgeRelations");

            migrationBuilder.DropTable(
                name: "ForgeReportColumns");

            migrationBuilder.DropTable(
                name: "ForgeReportFilters");

            migrationBuilder.DropTable(
                name: "ForgeReportGroups");

            migrationBuilder.DropTable(
                name: "ForgeReportSignatures");

            migrationBuilder.DropTable(
                name: "ForgeReportSummaries");

            migrationBuilder.DropTable(
                name: "ForgeTreeLevels");

            migrationBuilder.DropTable(
                name: "LookupConfigurations");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "ForgeReports");

            migrationBuilder.DropTable(
                name: "ForgeForms");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "EmailChannels");

            migrationBuilder.DropTable(
                name: "EmailRetryPolicies");
        }
    }
}
