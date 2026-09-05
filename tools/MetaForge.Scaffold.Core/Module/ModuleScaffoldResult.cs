namespace MetaForge.Scaffold.Module;

public sealed class ModuleScaffoldResult
{
    public required string ModuleName { get; init; }

    public required string SchemaName { get; init; }

    public IReadOnlyList<string> WrittenFiles { get; init; } = [];

    public IReadOnlyList<string> PlannedFiles { get; init; } = [];

    public IReadOnlyList<string> PatchedFiles { get; init; } = [];

    public bool DryRun { get; init; }

    public string? MigrationName { get; init; }

    public string? MigrationOutput { get; init; }

    public IReadOnlyDictionary<string, string> SourcePreviews { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
