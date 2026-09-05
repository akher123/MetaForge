using System.Text;
using Humanizer;
using MetaForge.Scaffold.Config;
using MetaForge.Scaffold.Models;

namespace MetaForge.Scaffold.Generation;

public static class EntityCodeGenerator
{
    public static string Generate(TableModel table, ModuleScaffoldProfile profile, bool includeNavigations)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {profile.EntityNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {table.EntityName} entity for module {profile.Name} (schema: {profile.SchemaName}).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {table.EntityName} : {ResolveBaseEntityType(table)}");
        sb.AppendLine("{");

        foreach (var column in table.Columns.Where(c => !c.IsPrimaryKey))
        {
            if (includeNavigations && column.IsForeignKey && column.ReferencedTable != null
                && !column.Name.EndsWith("Id", StringComparison.Ordinal))
                continue;

            var clr = ToPropertyClrType(column);
            var defaultInit = GetDefaultInitializer(clr, column.IsNullable);
            sb.AppendLine($"    public {clr} {column.Name} {{ get; set; }}{defaultInit}");
        }

        if (includeNavigations)
        {
            foreach (var fk in table.Columns.Where(c => c.IsForeignKey && c.ReferencedTable != null && c.Name.EndsWith("Id")))
            {
                var navName = fk.Name[..^2];
                var refEntity = SingularizeTable(fk.ReferencedTable!);
                sb.AppendLine($"    public {refEntity}? {navName} {{ get; set; }}");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string SingularizeTable(string tableName) =>
        tableName.Singularize(inputIsKnownToBePlural: true);

    private static string ResolveBaseEntityType(TableModel table)
    {
        var pk = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
        var keyClr = (pk?.ClrTypeName ?? "int").TrimEnd('?');
        return string.Equals(keyClr, "int", StringComparison.OrdinalIgnoreCase)
            ? "BaseEntity"
            : $"BaseEntity<{keyClr}>";
    }

    private static string ToPropertyClrType(ColumnModel column)
    {
        var clr = column.ClrTypeName.TrimEnd('?');
        if (column.IsNullable)
            return clr + "?";
        return clr;
    }

    private static string GetDefaultInitializer(string clr, bool isNullable)
    {
        if (isNullable || clr.EndsWith('?'))
            return string.Empty;

        return clr switch
        {
            "string" => " = string.Empty;",
            "bool" => " = false;",
            _ when clr.StartsWith("int", StringComparison.Ordinal) => " = 0;",
            _ when clr.StartsWith("long", StringComparison.Ordinal) => " = 0L;",
            _ when clr.StartsWith("decimal", StringComparison.Ordinal) => " = 0m;",
            _ when clr.StartsWith("double", StringComparison.Ordinal) => " = 0d;",
            _ when clr.StartsWith("float", StringComparison.Ordinal) => " = 0f;",
            _ => string.Empty
        };
    }
}
