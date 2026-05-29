namespace MetaForge.Scaffold.Models;

public sealed class TableModel
{
    public required string TableName { get; init; }

    public required string EntityName { get; init; }

    public required IReadOnlyList<ColumnModel> Columns { get; init; }
}
