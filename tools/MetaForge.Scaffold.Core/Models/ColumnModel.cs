namespace MetaForge.Scaffold.Models;

public sealed class ColumnModel
{
    public required string Name { get; init; }

    public required string ClrTypeName { get; init; }

    public bool IsNullable { get; init; }

    public bool IsPrimaryKey { get; init; }

    public bool IsForeignKey { get; init; }

    public string? ReferencedTable { get; init; }

    public int? MaxLength { get; init; }

    public int? Precision { get; init; }

    public int? Scale { get; init; }

    public bool IsUnicode { get; init; } = true;
}
