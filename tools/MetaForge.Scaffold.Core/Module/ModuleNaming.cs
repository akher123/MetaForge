using System.Text.RegularExpressions;
using MetaForge.Scaffold;

namespace MetaForge.Scaffold.Module;

public sealed class ModuleNaming
{
    private static readonly Regex ValidNamePattern = new(@"^[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    public string Name { get; }

    public string FolderName => Name;

    public string SchemaName => Name.ToLowerInvariant();

    public string DomainProject => $"MetaForge.{Name}.Domain";

    public string ApplicationProject => $"MetaForge.{Name}.Application";

    public string InfrastructureProject => $"MetaForge.{Name}.Infrastructure";

    public string DbContextName => $"{Name}DbContext";

    public string EntityNamespace => $"MetaForge.{Name}.Domain.Entities";

    public string ConfigNamespace => $"MetaForge.{Name}.Infrastructure.Persistence.Configurations.Generated";

    public string ModuleClassName => $"{Name}Module";

    public string AddModuleMethodName => $"Add{Name}Module";

    public string ModuleFolder { get; }

    public string SolutionFolderName => $"{ScaffoldConstants.SolutionModulesFolderPrefix}{Name}/";

    public string DomainProjectPath => $"{ModuleFolder}/{DomainProject}/{DomainProject}.csproj";

    public string ApplicationProjectPath => $"{ModuleFolder}/{ApplicationProject}/{ApplicationProject}.csproj";

    public string InfrastructureProjectPath => $"{ModuleFolder}/{InfrastructureProject}/{InfrastructureProject}.csproj";

    public string DbContextPath => $"{ModuleFolder}/{InfrastructureProject}/Persistence/{DbContextName}.cs";

    public string DependencyInjectionPath => $"{ModuleFolder}/{InfrastructureProject}/DependencyInjection.cs";

    public string GlobalUsingsPath => $"{ModuleFolder}/{DomainProject}/GlobalUsings.cs";

    public string EntitiesFolder => $"{ModuleFolder}/{DomainProject}/Entities";

    public string GeneratedConfigFolder =>
        $"{ModuleFolder}/{InfrastructureProject}/Persistence/Configurations/Generated";

    public string MigrationsFolder => $"{ModuleFolder}/{InfrastructureProject}/Persistence/Migrations";

    public string WebAreaPath => $"src/MetaForge.Web/Areas/{Name}";

    public string WebAreaControllerPath => $"{WebAreaPath}/Controllers/HomeController.cs";

    public string WebAreaIndexViewPath => $"{WebAreaPath}/Views/Home/Index.cshtml";

    public string WebAreaViewStartPath => $"{WebAreaPath}/Views/_ViewStart.cshtml";

    public string InfrastructureNamespace => $"MetaForge.{Name}.Infrastructure";

    public string PersistenceNamespace => $"MetaForge.{Name}.Infrastructure.Persistence";

    public ModuleNaming(string moduleName, string? moduleFolder = null)
    {
        var trimmed = moduleName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Module name is required.", nameof(moduleName));

        if (!ValidNamePattern.IsMatch(trimmed))
            throw new ArgumentException(
                $"Module name '{trimmed}' is invalid. Use PascalCase starting with a letter (e.g. Production, Accounting).",
                nameof(moduleName));

        Name = trimmed;
        ModuleFolder = string.IsNullOrWhiteSpace(moduleFolder)
            ? $"{ScaffoldConstants.DefaultModuleFolderPrefix}/{Name}"
            : moduleFolder.Trim().Trim('/');
    }

    public static ModuleNaming Parse(string moduleName) => new(moduleName);

    public static ModuleNaming FromConfig(string moduleName, string moduleFolder) => new(moduleName, moduleFolder);
}
