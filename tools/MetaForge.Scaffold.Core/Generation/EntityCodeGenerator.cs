using System.Text;
using Humanizer;
using MetaForge.Scaffold.Models;

namespace MetaForge.Scaffold.Generation;

public static class EntityCodeGenerator
{
    public static string Generate(TableModel table, bool includeNavigations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace MetaForge.Domain.Features;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {table.EntityName} business entity (scaffolded from {table.TableName}).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {table.EntityName} : BaseEntity");
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

    private static string ToPropertyClrType(ColumnModel column)
    {
        var clr = column.ClrTypeName.TrimEnd('?');
        if (column.IsNullable)
            return clr == "string" ? "string?" : clr + "?";
        return clr;
    }

    private static string GetDefaultInitializer(string clrType, bool isNullable)
    {
        if (clrType == "string" && !isNullable)
            return " = string.Empty;";
        return "";
    }
}
