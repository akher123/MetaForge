namespace MetaForge.Scaffold;

public sealed class ScaffoldOptions
{
    public string? TableName { get; set; }

    public string? EntityName { get; set; }

    public string? Columns { get; set; }

    public string? ConnectionString { get; set; }

    public string? ConfigPath { get; set; }

    public string? ModuleName { get; set; }

    public string? MetaForgeConfigPath { get; set; }

    public string SolutionRoot { get; set; } = ".";

    public string DomainOutputDir { get; set; } = "src/Hrm/MetaForge.Hrm.Domain/Entities";

    public string ConfigOutputDir { get; set; } = "src/Hrm/MetaForge.Hrm.Infrastructure/Persistence/Configurations/Generated";

    public string DbContextPath { get; set; } = "src/Core/MetaForge.Core.Infrastructure/Persistence/MetaForgeDbContext.cs";

    public string InfrastructureProject { get; set; } = "src/Hrm/MetaForge.Hrm.Infrastructure/MetaForge.Hrm.Infrastructure.csproj";

    public string WebProject { get; set; } = ScaffoldConstants.WebProject;

    public string MigrationOutputDir { get; set; } = "Persistence/Migrations";

    public string DbContextName { get; set; } = "HrmDbContext";

    public string EntityNamespace { get; set; } = "MetaForge.Hrm.Domain.Entities";

    public string ConfigNamespace { get; set; } = "MetaForge.Hrm.Infrastructure.Persistence.Configurations.Generated";

    public string SchemaName { get; set; } = "hrm";

    public string? AreaName { get; set; }

    public bool IncludeNavigations { get; set; }

    public bool DryRun { get; set; }

    public bool NoDbSetPatch { get; set; }

    public bool NoDbSetPatchExplicit { get; set; }

    public bool AddMigration { get; set; }

    public bool Force { get; set; }

    public bool IsReverseFromTable => !string.IsNullOrWhiteSpace(TableName);

    public bool IsGreenfield =>
        !string.IsNullOrWhiteSpace(EntityName) && !string.IsNullOrWhiteSpace(Columns);
}
