using System.Text.Json;
using MetaForge.Scaffold;

namespace MetaForge.Scaffold.Config;

public static class MetaForgeConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static MetaForgeConfig Load(string solutionRoot, string? configFileName = null)
    {
        var fileName = configFileName ?? "metaforge.json";
        var path = Path.Combine(solutionRoot, fileName);
        if (!File.Exists(path))
            return CreateDefaultConfig();

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<MetaForgeConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse {path}.");

        if (config.Modules.Count == 0)
            throw new InvalidOperationException($"{path} does not define any modules.");

        return config;
    }

    public static void Save(MetaForgeConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, WriteJsonOptions);
        File.WriteAllText(path, json + Environment.NewLine);
    }

    public static IReadOnlyList<string> GetEnabledModuleNames(MetaForgeConfig config) =>
        config.EnabledModules.Count > 0
            ? config.EnabledModules
            : config.Modules.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static ModuleScaffoldProfile ResolveModule(MetaForgeConfig config, string moduleName, string solutionRoot)
    {
        if (!config.Modules.TryGetValue(moduleName, out var entry))
            throw new InvalidOperationException(
                $"Module '{moduleName}' is not defined in metaforge.json. Available: {string.Join(", ", config.Modules.Keys)}.");

        var folder = NormalizePath(entry.Folder);
        var domainProject = entry.DomainProject.Trim();
        var infraProject = entry.InfrastructureProject.Trim();
        var entityNamespace = entry.EntityNamespace.Trim();
        var schema = entry.SchemaName.Trim();

        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(domainProject) || string.IsNullOrEmpty(infraProject))
            throw new InvalidOperationException($"Module '{moduleName}' is missing folder or project names in metaforge.json.");

        var configNamespace = entry.ConfigNamespace?.Trim()
            ?? $"{ToRootNamespace(infraProject)}.Persistence.Configurations.Generated";

        var dbContextName = string.IsNullOrWhiteSpace(entry.DbContextName)
            ? $"{moduleName}DbContext"
            : entry.DbContextName.Trim();

        return new ModuleScaffoldProfile
        {
            Name = moduleName,
            AreaName = string.IsNullOrWhiteSpace(entry.AreaName) ? moduleName : entry.AreaName.Trim(),
            SchemaName = string.IsNullOrEmpty(schema) ? moduleName.ToLowerInvariant() : schema,
            EntityNamespace = entityNamespace,
            ConfigNamespace = configNamespace,
            DomainOutputDir = entry.DomainOutputDir?.Trim()
                ?? CombinePath(folder, domainProject, "Entities"),
            ConfigOutputDir = entry.ConfigOutputDir?.Trim()
                ?? CombinePath(folder, infraProject, "Persistence", "Configurations", "Generated"),
            DbContextName = dbContextName,
            DbContextPath = entry.DbContextPath?.Trim()
                ?? CombinePath(folder, infraProject, "Persistence", $"{dbContextName}.cs"),
            InfrastructureProjectPath = entry.InfrastructureProjectPath?.Trim()
                ?? CombinePath(folder, infraProject, $"{infraProject}.csproj"),
            WebProjectPath = string.IsNullOrWhiteSpace(entry.WebProjectPath)
                ? ScaffoldConstants.WebProject
                : entry.WebProjectPath.Trim(),
            PatchDbSet = true
        };

        static string CombinePath(params string[] parts) =>
            string.Join('/', parts.Select(p => p.Trim().Trim('/')));

        static string ToRootNamespace(string projectName) =>
            projectName.Replace("-", ".");
    }

    public static void ApplyToOptions(ScaffoldOptions options, ModuleScaffoldProfile profile, string solutionRoot)
    {
        options.ModuleName = profile.Name;
        options.DomainOutputDir = profile.DomainOutputDir;
        options.ConfigOutputDir = profile.ConfigOutputDir;
        options.InfrastructureProject = profile.InfrastructureProjectPath;
        options.WebProject = profile.WebProjectPath;
        options.DbContextName = profile.DbContextName;
        options.DbContextPath = profile.DbContextPath;
        options.EntityNamespace = profile.EntityNamespace;
        options.ConfigNamespace = profile.ConfigNamespace;
        options.SchemaName = profile.SchemaName;
        options.AreaName = profile.AreaName;

        if (!options.NoDbSetPatchExplicit && !profile.PatchDbSet)
            options.NoDbSetPatch = true;
    }

    private static MetaForgeConfig CreateDefaultConfig() =>
        new()
        {
            EnabledModules = ["Hrm"],
            Modules = new Dictionary<string, MetaForgeModuleEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Hrm"] = new()
                {
                    Folder = "src/Hrm",
                    AreaName = "Hrm",
                    SchemaName = "hrm",
                    DomainProject = "MetaForge.Hrm.Domain",
                    ApplicationProject = "MetaForge.Hrm.Application",
                    InfrastructureProject = "MetaForge.Hrm.Infrastructure",
                    DbContextName = "HrmDbContext",
                    EntityNamespace = "MetaForge.Hrm.Domain.Entities",
                    WebProjectPath = ScaffoldConstants.WebProject
                }
            }
        };

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim('/');
}
