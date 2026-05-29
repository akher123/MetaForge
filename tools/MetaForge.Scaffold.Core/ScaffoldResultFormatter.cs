using System.Text;

namespace MetaForge.Scaffold;

public static class ScaffoldResultFormatter
{
    public static string Format(ScaffoldResult result)
    {
        var sb = new StringBuilder();

        if (result.DryRun)
        {
            sb.AppendLine($"[Preview] Entity: {result.EntityName}, Table: {result.TableName}");
            foreach (var path in result.PlannedFiles)
                sb.AppendLine($"  Would write: {path}");
            if (result.WillPatchDbSet)
                sb.AppendLine("  Would patch DbSet in MetaForgeDbContext");
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

        sb.AppendLine($"Scaffolded {result.EntityName} (table: {result.TableName})");
        foreach (var file in result.WrittenFiles)
            sb.AppendLine($"  Written: {file}");

        if (result.DbSetPatched)
            sb.AppendLine("  DbSet added to MetaForgeDbContext");

        if (result.MigrationName != null)
            sb.AppendLine($"  Migration: {result.MigrationName}");

        if (!string.IsNullOrWhiteSpace(result.MigrationOutput))
        {
            sb.AppendLine();
            sb.AppendLine("--- Migration output ---");
            sb.AppendLine(result.MigrationOutput);
        }

        sb.AppendLine();
        sb.AppendLine("Next: dotnet build → run app → Form Builder → Auto-Build → Save → Menu");
        return sb.ToString();
    }
}
