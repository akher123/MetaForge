using System.Collections;
using System.Reflection;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Validates and resolves dotted property paths (e.g. SalesOrder.Customer.Name) against entity CLR types.
/// </summary>
internal static class ReportPropertyPathResolver
{
    public static bool IsValidPath(Type rootType, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return TryResolve(rootType, path.Trim(), out _, out _);
    }

    public static bool TryResolve(Type rootType, string path, out PropertyInfo? leafProperty, out IReadOnlyList<PropertyInfo> navigationProperties)
    {
        leafProperty = null;
        navigationProperties = [];

        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        var navigations = new List<PropertyInfo>();
        var currentType = rootType;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var prop = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return false;

            var isLast = i == segments.Length - 1;
            if (isLast)
            {
                if (!IsSimpleType(prop.PropertyType))
                    return false;

                leafProperty = prop;
                navigationProperties = navigations;
                return true;
            }

            if (!IsReferenceNavigation(prop.PropertyType))
                return false;

            navigations.Add(prop);
            currentType = prop.PropertyType;
        }

        return false;
    }

    public static string? GetIncludePath(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath) || !propertyPath.Contains('.', StringComparison.Ordinal))
            return null;

        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return null;

        return string.Join('.', segments[..^1]);
    }

    public static IReadOnlyList<string> GetMinimalIncludePaths(IEnumerable<string> propertyPaths)
    {
        var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in propertyPaths)
        {
            var include = GetIncludePath(path);
            if (string.IsNullOrWhiteSpace(include))
                continue;

            includes.Add(include);

            // Ensure parent includes exist for nested paths (EF accepts deepest path, but add intermediates for clarity).
            var segments = include.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 1; i <= segments.Length; i++)
                includes.Add(string.Join('.', segments.Take(i)));
        }

        return includes
            .OrderBy(p => p.Count(c => c == '.'))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsStringPath(Type rootType, string path)
    {
        return TryResolve(rootType, path, out var leaf, out _)
            && leaf?.PropertyType == typeof(string);
    }

    public static IReadOnlyList<ReportPropertyOption> DiscoverPaths(Type rootType, int maxDepth = 2)
    {
        var results = new List<ReportPropertyOption>();
        var visited = new HashSet<Type>();

        void Walk(Type type, string prefix, int depth)
        {
            if (depth > maxDepth)
                return;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                    continue;

                var path = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + prop.Name;

                if (IsSimpleType(prop.PropertyType))
                {
                    results.Add(new ReportPropertyOption
                    {
                        Path = path,
                        Label = SplitPascalPath(path),
                        ClrType = (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType).Name,
                        IsForeignKey = prop.Name.EndsWith("Id", StringComparison.Ordinal)
                            && !prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                    });
                    continue;
                }

                if (depth >= maxDepth || !IsReferenceNavigation(prop.PropertyType))
                    continue;

                if (!visited.Add(prop.PropertyType))
                    continue;

                Walk(prop.PropertyType, path + ".", depth + 1);
                visited.Remove(prop.PropertyType);
            }
        }

        Walk(rootType, string.Empty, 0);
        return results
            .OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsReferenceNavigation(Type type)
    {
        if (type == typeof(string))
            return false;

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return false;

        return type.IsClass;
    }

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid)
            || underlying.IsEnum;
    }

    private static string SplitPascalPath(string path) =>
        string.Join(" / ", path.Split('.').Select(SplitPascalCase));

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return string.Concat(value.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]) ? " " + c : c.ToString()));
    }
}

internal sealed class ReportPropertyOption
{
    public string Path { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ClrType { get; set; } = string.Empty;

    public bool IsForeignKey { get; set; }
}
