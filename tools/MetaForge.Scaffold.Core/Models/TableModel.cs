namespace MetaForge.Scaffold.Models;

public sealed class TableModel
{
    public string SchemaName { get; init; } = "dbo";

    public required string TableName { get; init; }

    public string QualifiedTableName => $"{SchemaName}.{TableName}";

    public required string EntityName { get; init; }

    public required IReadOnlyList<ColumnModel> Columns { get; init; }
}
