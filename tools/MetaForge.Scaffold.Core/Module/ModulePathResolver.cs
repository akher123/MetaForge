namespace MetaForge.Scaffold.Module;

public static class ModulePathResolver
{
    public static string GetRelativeProjectReference(string fromProjectDirectory, string targetProjectPath)
    {
        var fromDir = Path.GetFullPath(fromProjectDirectory);
        var targetFile = Path.GetFullPath(targetProjectPath);
        var relative = Path.GetRelativePath(fromDir, targetFile);
        return NormalizeCsprojPath(relative);
    }

    public static string GetRelativeProjectReferenceFromModuleProject(
        string solutionRoot,
        ModuleNaming naming,
        string projectName,
        string targetProjectPath)
    {
        var fromDir = Path.Combine(solutionRoot, naming.ModuleFolder, projectName);
        return GetRelativeProjectReference(fromDir, targetProjectPath);
    }

    public static string ResolveFromSolutionRoot(string solutionRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(solutionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string NormalizeCsprojPath(string path) =>
        path.Replace('\\', '/');
}
