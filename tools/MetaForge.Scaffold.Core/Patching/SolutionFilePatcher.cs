using MetaForge.Scaffold;

namespace MetaForge.Scaffold.Patching;

public static class SolutionFilePatcher
{
    public static bool TryAddModule(string slnxPath, Module.ModuleNaming naming, out string? error)
    {
        error = null;
        if (!File.Exists(slnxPath))
        {
            error = $"Solution file not found: {slnxPath}";
            return false;
        }

        var content = File.ReadAllText(slnxPath);
        var folderMarker = $"<Folder Name=\"{naming.SolutionFolderName}\">";
        if (content.Contains(folderMarker, StringComparison.Ordinal))
        {
            error = $"Module folder '{naming.SolutionFolderName}' already exists in solution.";
            return false;
        }

        var block =
            $"""
              <Folder Name="{naming.SolutionFolderName}">
                <Project Path="{naming.DomainProjectPath}" />
                <Project Path="{naming.ApplicationProjectPath}" />
                <Project Path="{naming.InfrastructureProjectPath}" />
              </Folder>

            """;

        var insertBefore = FindInsertMarker(content);
        if (insertBefore == null)
        {
            error = "Could not find a solution folder marker to insert the new module before.";
            return false;
        }

        var index = content.IndexOf(insertBefore, StringComparison.Ordinal);
        var updated = content.Insert(index, block);
        File.WriteAllText(slnxPath, updated);
        return true;
    }

    private static string? FindInsertMarker(string content)
    {
        if (content.Contains(ScaffoldConstants.SolutionHostsFolder, StringComparison.Ordinal))
            return $"  <Folder Name=\"{ScaffoldConstants.SolutionHostsFolder}\">";

        if (content.Contains("  <Folder Name=\"/tools/\">", StringComparison.Ordinal))
            return "  <Folder Name=\"/tools/\">";

        return null;
    }
}
