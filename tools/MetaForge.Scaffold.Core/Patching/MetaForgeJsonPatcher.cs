using System.Text.Json;
using MetaForge.Scaffold.Config;

namespace MetaForge.Scaffold.Patching;

public static class MetaForgeJsonPatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static bool TryAddModule(string configPath, Module.ModuleNaming naming, out string? error)
    {
        error = null;
        if (!File.Exists(configPath))
        {
            error = $"Config file not found: {configPath}";
            return false;
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<MetaForgeConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse {configPath}.");

        if (config.Modules.ContainsKey(naming.Name))
        {
            error = $"Module '{naming.Name}' already exists in metaforge.json.";
            return false;
        }

        config.Modules[naming.Name] = new MetaForgeModuleEntry
        {
            Folder = naming.ModuleFolder,
            AreaName = naming.Name,
            SchemaName = naming.SchemaName,
            DomainProject = naming.DomainProject,
            ApplicationProject = naming.ApplicationProject,
            InfrastructureProject = naming.InfrastructureProject,
            DbContextName = naming.DbContextName,
            EntityNamespace = naming.EntityNamespace,
            WebAreaPath = naming.WebAreaPath
        };

        if (!config.EnabledModules.Contains(naming.Name, StringComparer.OrdinalIgnoreCase))
            config.EnabledModules.Add(naming.Name);

        MetaForgeConfigLoader.Save(config, configPath);
        return true;
    }

    public static bool ModuleExists(string configPath, string moduleName)
    {
        if (!File.Exists(configPath))
            return false;

        var root = Path.GetDirectoryName(configPath)!;
        var config = MetaForgeConfigLoader.Load(root, Path.GetFileName(configPath));
        return config.Modules.ContainsKey(moduleName);
    }
}
