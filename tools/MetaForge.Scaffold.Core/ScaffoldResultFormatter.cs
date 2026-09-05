using System.Text;

namespace MetaForge.Scaffold;

public static class ScaffoldResultFormatter
{
    public static string Format(ScaffoldResult result)
    {
        var sb = new StringBuilder();
        var moduleLabel = string.IsNullOrWhiteSpace(result.ModuleName) ? "" : $" [{result.ModuleName} / schema: {result.SchemaName}]";

        if (result.DryRun)
        {
            sb.AppendLine($"[Preview]{moduleLabel} Entity: {result.EntityName}, Table: {result.QualifiedTableName}");
            foreach (var path in result.PlannedFiles)
                sb.AppendLine($"  Would write: {path}");
            if (result.WillPatchDbSet)
                sb.AppendLine("  Would patch DbSet in DbContext");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.EntitySourcePreview))
            {
                sb.AppendLine("--- Entity preview ---");
                sb.AppendLine(result.EntitySourcePreview);
            }

            if (!string.IsNullOrEmpty(result.ConfigurationSourcePreview))
            {
                sb.AppendLine("--- Configuration preview ---");
                sb.AppendLine(result.ConfigurationSourcePreview);
            }

            sb.AppendLine("[Preview] No files were written.");
            return sb.ToString();
        }

        sb.AppendLine($"Scaffolded {result.EntityName} (table: {result.QualifiedTableName}){moduleLabel}");
        foreach (var file in result.WrittenFiles)
            sb.AppendLine($"  Written: {file}");

        if (result.DbSetPatched)
            sb.AppendLine($"  DbSet added to {result.DbContextName ?? "DbContext"}");

        if (result.MigrationName != null)
            sb.AppendLine($"  Migration: {result.MigrationName} ({result.ModuleName} / {result.SchemaName})");

        if (!string.IsNullOrWhiteSpace(result.MigrationOutput))
        {
            sb.AppendLine();
            sb.AppendLine("--- Migration output ---");
            sb.AppendLine(result.MigrationOutput);
        }

        sb.AppendLine();
        sb.AppendLine("Next: dotnet build → run app → Form Builder → Auto-Build → Menu → Permissions");
        return sb.ToString();
    }
}
