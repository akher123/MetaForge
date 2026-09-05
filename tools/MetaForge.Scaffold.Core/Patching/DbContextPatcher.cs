using System.Text.RegularExpressions;

namespace MetaForge.Scaffold.Patching;

public static class DbContextPatcher
{
    private static readonly Regex DbSetPattern = new(
        @"public\s+DbSet<\s*(\w+)\s*>\s+(\w+)\s*=>",
        RegexOptions.Compiled);

    public static bool TryPatch(
        string dbContextPath,
        string entityName,
        string pluralPropertyName,
        string? entityNamespace,
        out string? error)
    {
        error = null;
        if (!File.Exists(dbContextPath))
        {
            error = $"DbContext file not found: {dbContextPath}";
            return false;
        }

        var content = File.ReadAllText(dbContextPath);
        if (content.Contains($"DbSet<{entityName}>", StringComparison.Ordinal))
        {
            error = $"DbSet<{entityName}> already exists in DbContext.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entityNamespace))
        {
            var usingLine = $"using {entityNamespace.Trim()};";
            if (!content.Contains(usingLine, StringComparison.Ordinal))
            {
                const string namespaceMarker = "namespace ";
                var namespaceIndex = content.IndexOf(namespaceMarker, StringComparison.Ordinal);
                if (namespaceIndex < 0)
                {
                    error = "Could not find namespace marker in DbContext file.";
                    return false;
                }

                content = content.Insert(namespaceIndex, usingLine + Environment.NewLine);
            }
        }

        const string marker = "    protected override void OnModelCreating(ModelBuilder modelBuilder)";
        var index = content.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            error = "Could not find OnModelCreating marker in DbContext.";
            return false;
        }

        var line = $"    public DbSet<{entityName}> {pluralPropertyName} => Set<{entityName}>();{Environment.NewLine}{Environment.NewLine}";
        var updated = content.Insert(index, line);
        File.WriteAllText(dbContextPath, updated);
        return true;
    }

    public static bool DbSetExists(string dbContextPath, string entityName)
    {
        if (!File.Exists(dbContextPath))
            return false;

        var content = File.ReadAllText(dbContextPath);
        return content.Contains($"DbSet<{entityName}>", StringComparison.Ordinal);
    }
}
