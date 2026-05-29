using Humanizer;
using MetaForge.Scaffold.Models;

namespace MetaForge.Scaffold.Generation;

/// <summary>
/// Parses greenfield column specs: Name:type[:length][!] e.g. Code:string:50!, Name:string:200, IsActive:bool
/// </summary>
public static class ColumnSpecParser
{
    public static TableModel Parse(string entityName, string tableName, string columnsSpec)
    {
        var columns = new List<ColumnModel> { CreateIdColumn() };

        foreach (var part in columnsSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            columns.Add(ParseColumn(part));
        }

        return new TableModel
        {
            TableName = tableName,
            EntityName = entityName,
            Columns = columns
        };
    }

    private static ColumnModel CreateIdColumn() =>
        new()
        {
            Name = "Id",
            ClrTypeName = "int",
            IsNullable = false,
            IsPrimaryKey = true,
            IsForeignKey = false
        };

    private static ColumnModel ParseColumn(string spec)
    {
        var required = spec.EndsWith('!');
        if (required)
            spec = spec[..^1];

        var segments = spec.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            throw new FormatException($"Invalid column spec '{spec}'. Expected Name:type[:length][!].");

        var name = segments[0];
        var type = segments[1].ToLowerInvariant();
        int? maxLength = null;
        int? precision = null;
        int? scale = null;

        if (segments.Length >= 3)
        {
            var size = segments[2];
            if (size.Contains(','))
            {
                var ps = size.Split(',', 2);
                precision = int.Parse(ps[0]);
                scale = int.Parse(ps[1]);
            }
            else if (int.TryParse(size, out var len))
            {
                maxLength = len;
            }
        }

        var (clrType, isUnicode) = MapSpecType(type);
        var isNullable = !required && clrType != "string";

        if (clrType == "string" && !required)
            isNullable = true;

        return new ColumnModel
        {
            Name = name,
            ClrTypeName = isNullable && clrType != "string" ? clrType + "?" : clrType,
            IsNullable = isNullable,
            IsPrimaryKey = false,
            IsForeignKey = name.EndsWith("Id", StringComparison.Ordinal) && name != "Id",
            ReferencedTable = name.EndsWith("Id", StringComparison.Ordinal) && name != "Id"
                ? name[..^2].Pluralize(inputIsKnownToBeSingular: true)
                : null,
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale,
            IsUnicode = isUnicode
        };
    }

    private static (string ClrType, bool IsUnicode) MapSpecType(string type) =>
        type switch
        {
            "string" => ("string", true),
            "int" => ("int", true),
            "long" => ("long", true),
            "bool" or "boolean" => ("bool", true),
            "decimal" => ("decimal", true),
            "double" => ("double", true),
            "float" => ("float", true),
            "datetime" => ("DateTime", true),
            "date" => ("DateTime", true),
            "guid" => ("Guid", true),
            _ => throw new FormatException($"Unknown column type '{type}'.")
        };

    public static string DefaultTableName(string entityName) =>
        entityName.Pluralize();
}
