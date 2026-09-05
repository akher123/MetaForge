namespace MetaForge.Scaffold.Schema;

public readonly record struct TableIdentifier(string Schema, string TableName)
{
    public string QualifiedName => $"{Schema}.{TableName}";

    public static TableIdentifier Parse(string tableInput, string? moduleSchema)
    {
        var trimmed = tableInput.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Table name is required.", nameof(tableInput));

        var dot = trimmed.IndexOf('.');
        if (dot >= 0)
        {
            var schema = trimmed[..dot].Trim();
            var tableName = trimmed[(dot + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException(
                    $"Invalid table identifier '{tableInput}'. Use 'schema.table' or 'TableName'.",
                    nameof(tableInput));
            }

            return new TableIdentifier(schema, tableName);
        }

        var name = trimmed;
        var resolvedSchema = string.IsNullOrWhiteSpace(moduleSchema) ? "dbo" : moduleSchema.Trim();
        return new TableIdentifier(resolvedSchema, name);
    }
}
