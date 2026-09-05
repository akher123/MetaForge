namespace MetaForge.Scaffold;

public sealed class ScaffoldResult
{
    public string? ModuleName { get; init; }

    public string? SchemaName { get; init; }

    public required string EntityName { get; init; }

    public required string TableName { get; init; }

    public string? TableSchemaName { get; init; }

    public string QualifiedTableName =>
        string.IsNullOrWhiteSpace(TableSchemaName) ? TableName : $"{TableSchemaName}.{TableName}";

    public IReadOnlyList<string> WrittenFiles { get; init; } = [];

    public IReadOnlyList<string> PlannedFiles { get; init; } = [];

    public bool DbSetPatched { get; init; }

    public string? DbContextName { get; init; }

    public bool WillPatchDbSet { get; init; }

    public string? MigrationName { get; init; }

    public string? MigrationOutput { get; init; }

    public bool DryRun { get; init; }

    public string? EntitySourcePreview { get; init; }

    public string? ConfigurationSourcePreview { get; init; }
}
