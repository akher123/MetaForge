using MetaForge.Scaffold.Module;

namespace MetaForge.Scaffold.Patching;

public static class WebProjectReferencePatcher
{
    public static bool TryAddModuleReference(
        string csprojPath,
        Module.ModuleNaming naming,
        string solutionRoot,
        out string? error)
    {
        error = null;
        if (!File.Exists(csprojPath))
        {
            error = $"Web project not found: {csprojPath}";
            return false;
        }

        var content = File.ReadAllText(csprojPath);
        if (content.Contains(naming.InfrastructureProject, StringComparison.Ordinal))
        {
            error = $"Reference to {naming.InfrastructureProject} already exists.";
            return false;
        }

        const string endMarker = "  </ItemGroup>";
        var index = content.IndexOf(endMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            error = "Could not find ItemGroup in MetaForge.Web.csproj.";
            return false;
        }

        var webDir = Path.GetDirectoryName(csprojPath)!;
        var infraPath = ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, naming.InfrastructureProjectPath);
        var referencePath = ModulePathResolver.GetRelativeProjectReference(webDir, infraPath);
        var line = $"    <ProjectReference Include=\"{referencePath}\" />{Environment.NewLine}";
        var updated = content.Insert(index, line);
        File.WriteAllText(csprojPath, updated);
        return true;
    }
}
