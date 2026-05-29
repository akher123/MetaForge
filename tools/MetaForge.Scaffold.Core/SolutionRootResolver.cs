namespace MetaForge.Scaffold;

public static class SolutionRootResolver
{
    public static string Resolve(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var full = Path.GetFullPath(explicitRoot);
            if (!File.Exists(Path.Combine(full, "MetaForge.slnx")))
                throw new InvalidOperationException($"Solution root '{full}' does not contain MetaForge.slnx.");
            return full;
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MetaForge.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not find solution root (MetaForge.slnx). Run from the repo root or pass --root.");
    }
}
