using System.Text;
using Humanizer;
using MetaForge.Scaffold.Models;

namespace MetaForge.Scaffold.Generation;

public static class ConfigurationCodeGenerator
{
    public static string Generate(TableModel table, bool includeNavigations)
    {
        var entity = table.EntityName;
        var sb = new StringBuilder();
        sb.AppendLine("using MetaForge.Domain.Features;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Metadata.Builders;");
        sb.AppendLine();
        sb.AppendLine("namespace MetaForge.Infrastructure.Persistence.Configurations.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity}Configuration : IEntityTypeConfiguration<{entity}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Configure(EntityTypeBuilder<{entity}> builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.ToTable(\"{table.TableName}\");");
        sb.AppendLine("        builder.HasKey(x => x.Id);");

        foreach (var column in table.Columns.Where(c => !c.IsPrimaryKey))
        {
            if (includeNavigations && column.IsForeignKey && !column.Name.EndsWith("Id", StringComparison.Ordinal))
                continue;

            var propertyLines = BuildPropertyConfiguration(column);
            foreach (var line in propertyLines)
                sb.AppendLine($"        {line}");
        }

        if (includeNavigations)
        {
            foreach (var fk in table.Columns.Where(c =>
                         c.IsForeignKey && c.ReferencedTable != null && c.Name.EndsWith("Id", StringComparison.Ordinal)))
            {
                var navName = fk.Name[..^2];
                var refEntity = fk.ReferencedTable!.Singularize(inputIsKnownToBePlural: true);
                sb.AppendLine($"        builder.HasOne(x => x.{navName}).WithMany().HasForeignKey(x => x.{fk.Name}).OnDelete(DeleteBehavior.Restrict);");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<string> BuildPropertyConfiguration(ColumnModel column)
    {
        var chain = new List<string> { $"builder.Property(x => x.{column.Name})" };

        if (column.ClrTypeName.Contains("string", StringComparison.Ordinal))
        {
            if (column.MaxLength is > 0 and <= 8000)
                chain.Add($".HasMaxLength({column.MaxLength})");

            if (!column.IsUnicode)
                chain.Add(".IsUnicode(false)");
        }

        if (column.ClrTypeName.Contains("decimal", StringComparison.Ordinal)
            && column.Precision is int p && column.Scale is int s)
            chain.Add($".HasPrecision({p}, {s})");
        else if (column.ClrTypeName.Contains("decimal", StringComparison.Ordinal))
            chain.Add(".HasPrecision(18, 2)");

        if (!column.IsNullable && column.ClrTypeName != "string?")
            chain.Add(".IsRequired()");

        yield return string.Concat(chain) + ";";
    }
}
