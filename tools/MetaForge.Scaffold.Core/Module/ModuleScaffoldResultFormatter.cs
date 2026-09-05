using System.Text;

namespace MetaForge.Scaffold.Module;

public static class ModuleScaffoldResultFormatter
{
    public static string Format(ModuleScaffoldResult result)
    {
        var sb = new StringBuilder();

        if (result.DryRun)
        {
            sb.AppendLine($"[Preview] Module: {result.ModuleName} (schema: {result.SchemaName})");
            sb.AppendLine();
            sb.AppendLine("Would create:");
            foreach (var path in result.PlannedFiles)
                sb.AppendLine($"  {path}");

            sb.AppendLine();
            sb.AppendLine("Would patch:");
            foreach (var path in result.PatchedFiles)
                sb.AppendLine($"  {path}");

            foreach (var (path, source) in result.SourcePreviews)
            {
                sb.AppendLine();
                sb.AppendLine($"--- {path} ---");
                sb.AppendLine(source);
            }

            sb.AppendLine();
            sb.AppendLine("[Preview] No files were written.");
            return sb.ToString();
        }

        sb.AppendLine($"Created module {result.ModuleName} (schema: {result.SchemaName})");
        sb.AppendLine();
        sb.AppendLine("Written:");
        foreach (var file in result.WrittenFiles)
            sb.AppendLine($"  {file}");

        sb.AppendLine();
        sb.AppendLine("Patched:");
        foreach (var file in result.PatchedFiles)
            sb.AppendLine($"  {file}");

        if (result.MigrationName != null)
            sb.AppendLine($"  Migration: {result.MigrationName}");

        if (!string.IsNullOrWhiteSpace(result.MigrationOutput))
        {
            sb.AppendLine();
            sb.AppendLine("--- Migration output ---");
            sb.AppendLine(result.MigrationOutput);
        }

        sb.AppendLine();
        sb.AppendLine("Next: dotnet build → run app → Entity scaffold → Form Builder → Auto-Build");
        return sb.ToString();
    }
}
