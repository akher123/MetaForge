namespace MetaForge.Scaffold;

public sealed class ScaffoldOptions
{
    public string? TableName { get; set; }

    public string? EntityName { get; set; }

    public string? Columns { get; set; }

    public string? ConnectionString { get; set; }

    public string? ConfigPath { get; set; }

    public string SolutionRoot { get; set; } = ".";

    public string DomainOutputDir { get; set; } = "src/MetaForge.Domain/Features";

    public string ConfigOutputDir { get; set; } = "src/MetaForge.Infrastructure/Persistence/Configurations/Generated";

    public string DbContextPath { get; set; } = "src/MetaForge.Infrastructure/Persistence/MetaForgeDbContext.cs";

    public string InfrastructureProject { get; set; } = "src/MetaForge.Infrastructure/MetaForge.Infrastructure.csproj";

    public string WebProject { get; set; } = "src/MetaForge.Web/MetaForge.Web.csproj";

    public string MigrationOutputDir { get; set; } = "Persistence/Migrations";

    public string DbContextName { get; set; } = "MetaForgeDbContext";

    public bool IncludeNavigations { get; set; }

    public bool DryRun { get; set; }

    public bool NoDbSetPatch { get; set; }

    public bool AddMigration { get; set; }

    public bool Force { get; set; }

    public bool IsReverseFromTable => !string.IsNullOrWhiteSpace(TableName);

    public bool IsGreenfield =>
        !string.IsNullOrWhiteSpace(EntityName) && !string.IsNullOrWhiteSpace(Columns);
}
