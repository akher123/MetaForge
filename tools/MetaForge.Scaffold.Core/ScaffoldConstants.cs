namespace MetaForge.Scaffold;

public static class ScaffoldConstants
{
    public const string SharedProject = "src/MetaForge.Shared/MetaForge.Shared.csproj";
    public const string ModulesAbstractionsProject = "src/MetaForge.Modules.Abstractions/MetaForge.Modules.Abstractions.csproj";
    public const string CoreDomainProject = "src/Core/MetaForge.Core.Domain/MetaForge.Core.Domain.csproj";
    public const string CoreApplicationProject = "src/Core/MetaForge.Core.Application/MetaForge.Core.Application.csproj";
    public const string WebProject = "src/MetaForge.Web/MetaForge.Web.csproj";
    public const string ModuleRegistrationFile = "src/MetaForge.Web/Modules/MetaForgeModuleRegistration.cs";
    public const string SolutionModulesFolderPrefix = "/src/Modules/";
    public const string SolutionHostsFolder = "/src/Hosts/";
    public const string DefaultModuleFolderPrefix = "src/Modules";

    public static readonly HashSet<string> BlockedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
        "ForgeForms",
        "ForgeFields",
        "ForgeRelations",
        "ForgeGridColumns",
        "ForgeFormActions",
        "ForgeReports",
        "ForgeReportColumns",
        "ForgeReportFilters",
        "ForgeReportGroups",
        "ForgeReportSummaries",
        "ForgeMenus",
        "LookupConfigurations",
        "Users",
        "Roles",
        "Permissions",
        "UserRoles",
        "RolePermissions",
        "AuditLogs"
    };
}
