using System.Reflection;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Projects entity graphs into flat row dictionaries using configured property paths.
/// </summary>
internal static class ReportNavigationMapper
{
    public static Dictionary<string, object?> ToDictionary(object entity, IEnumerable<string> propertyPaths)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var rootType = entity.GetType();

        foreach (var path in propertyPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ReportPropertyPathResolver.TryResolve(rootType, path, out var leaf, out var navigations))
                continue;

            object? current = entity;
            foreach (var navigation in navigations)
            {
                current = navigation.GetValue(current);
                if (current == null)
                    break;
            }

            result[path] = current == null || leaf == null ? null : leaf.GetValue(current);
        }

        return result;
    }
}
