namespace MetaForge.Scaffold.Config;

using MetaForge.Scaffold;

public sealed class MetaForgeConfig
{
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    public string DatabaseName { get; set; } = "MetaForgeDb";

    public List<string> EnabledModules { get; set; } = [];

    public Dictionary<string, MetaForgeModuleEntry> Modules { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MetaForgeModuleEntry
{
    public string Folder { get; set; } = string.Empty;

    public string AreaName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string DomainProject { get; set; } = string.Empty;

    public string ApplicationProject { get; set; } = string.Empty;

    public string InfrastructureProject { get; set; } = string.Empty;

    public string DbContextName { get; set; } = string.Empty;

    public string EntityNamespace { get; set; } = string.Empty;

    public string? ConfigNamespace { get; set; }

    public string? DomainOutputDir { get; set; }

    public string? ConfigOutputDir { get; set; }

    public string? InfrastructureProjectPath { get; set; }

    public string? DbContextPath { get; set; }

    public string WebProjectPath { get; set; } = ScaffoldConstants.WebProject;

    public string? WebAreaPath { get; set; }
}

public sealed class ModuleScaffoldProfile
{
    public required string Name { get; init; }

    public required string AreaName { get; init; }

    public required string SchemaName { get; init; }

    public required string EntityNamespace { get; init; }

    public required string ConfigNamespace { get; init; }

    public required string DomainOutputDir { get; init; }

    public required string ConfigOutputDir { get; init; }

    public required string DbContextName { get; init; }

    public required string DbContextPath { get; init; }

    public required string InfrastructureProjectPath { get; init; }

    public required string WebProjectPath { get; init; }

    public bool PatchDbSet { get; init; }
}
