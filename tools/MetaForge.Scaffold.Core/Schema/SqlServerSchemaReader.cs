using Humanizer;
using MetaForge.Scaffold.Models;
using Microsoft.Data.SqlClient;

namespace MetaForge.Scaffold.Schema;

public sealed class SqlServerSchemaReader
{
    public async Task<TableModel> ReadTableAsync(
        string connectionString,
        string schema,
        string tableName,
        string? entityNameOverride,
        CancellationToken cancellationToken = default)
    {
        if (ScaffoldConstants.BlockedTables.Contains(tableName))
            throw new InvalidOperationException($"Table '{tableName}' is blocked from scaffolding (system/metadata table).");

        var qualifiedName = $"{schema}.{tableName}";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, schema, tableName, cancellationToken))
            throw new InvalidOperationException($"Table '{qualifiedName}' was not found in the database.");

        var columns = await ReadColumnsAsync(connection, schema, tableName, cancellationToken);
        if (columns.Count == 0)
            throw new InvalidOperationException($"Table '{qualifiedName}' has no columns.");

        ValidatePrimaryKey(columns, qualifiedName);

        var entityName = entityNameOverride
            ?? tableName.Singularize(inputIsKnownToBePlural: true);

        return new TableModel
        {
            SchemaName = schema,
            TableName = tableName,
            EntityName = entityName,
            Columns = columns
        };
    }

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @tableName
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static async Task<List<ColumnModel>> ReadColumnsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string columnSql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_SCHEMA = @schema
            ) pk ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA
                AND pk.TABLE_NAME = c.TABLE_NAME
                AND pk.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION
            """;

        var columns = new List<ColumnModel>();
        await using (var cmd = new SqlCommand(columnSql, connection))
        {
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sqlType = reader.GetString(1);
                var maxLength = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2));
                if (maxLength is < 0)
                    maxLength = null;

                var precision = reader.IsDBNull(3) ? (int?)null : Convert.ToInt32(reader.GetValue(3));
                var scale = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4));

                columns.Add(new ColumnModel
                {
                    Name = reader.GetString(0),
                    ClrTypeName = MapSqlTypeToClr(sqlType, maxLength, precision, scale, reader.GetString(5) == "YES"),
                    IsNullable = reader.GetString(5) == "YES",
                    IsPrimaryKey = reader.GetInt32(6) == 1,
                    MaxLength = maxLength,
                    Precision = precision,
                    Scale = scale,
                    IsUnicode = sqlType is "nvarchar" or "nchar" or "ntext"
                });
            }
        }

        await ApplyForeignKeysAsync(connection, schema, tableName, columns, cancellationToken);
        return columns;
    }

    private static async Task ApplyForeignKeysAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        List<ColumnModel> columns,
        CancellationToken cancellationToken)
    {
        const string fkSql = """
            SELECT
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
                OBJECT_NAME(fkc.referenced_object_id) AS ReferencedTable
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
            WHERE t.name = @tableName AND SCHEMA_NAME(t.schema_id) = @schema
            """;

        var fkLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new SqlCommand(fkSql, connection))
        {
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                fkLookup[reader.GetString(0)] = reader.GetString(1);
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!fkLookup.TryGetValue(col.Name, out var referencedTable))
                continue;

            columns[i] = new ColumnModel
            {
                Name = col.Name,
                ClrTypeName = col.ClrTypeName,
                IsNullable = col.IsNullable,
                IsPrimaryKey = col.IsPrimaryKey,
                IsForeignKey = true,
                ReferencedTable = referencedTable,
                MaxLength = col.MaxLength,
                Precision = col.Precision,
                Scale = col.Scale,
                IsUnicode = col.IsUnicode
            };
        }
    }

    private static void ValidatePrimaryKey(IReadOnlyList<ColumnModel> columns, string qualifiedTableName)
    {
        var pk = columns.FirstOrDefault(c => c.IsPrimaryKey);
        if (pk == null)
            throw new InvalidOperationException($"Table '{qualifiedTableName}' has no primary key.");

        if (!string.Equals(pk.Name, "Id", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Table '{qualifiedTableName}' primary key must be named 'Id' (found '{pk.Name}').");

        var keyClr = pk.ClrTypeName.TrimEnd('?');
        if (!AllowedPrimaryKeyClrTypes.Contains(keyClr))
        {
            throw new InvalidOperationException(
                $"Table '{qualifiedTableName}' primary key type '{pk.ClrTypeName}' is not supported. Allowed: {string.Join(", ", AllowedPrimaryKeyClrTypes)}.");
        }
    }

    private static readonly HashSet<string> AllowedPrimaryKeyClrTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int",
        "long",
        "Guid",
        "string"
    };

    private static string MapSqlTypeToClr(string sqlType, int? maxLength, int? precision, int? scale, bool isNullable)
    {
        var clr = sqlType.ToLowerInvariant() switch
        {
            "int" => "int",
            "bigint" => "long",
            "smallint" => "short",
            "tinyint" => "byte",
            "bit" => "bool",
            "decimal" or "numeric" or "money" => "decimal",
            "float" => "double",
            "real" => "float",
            "datetime" or "datetime2" or "smalldatetime" => "DateTime",
            "date" => "DateTime",
            "uniqueidentifier" => "Guid",
            "nvarchar" or "varchar" or "nchar" or "char" or "text" or "ntext" => "string",
            _ => "string"
        };

        if (isNullable && clr != "string")
            clr += "?";

        return clr;
    }
}
