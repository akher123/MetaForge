using MetaForge.Infrastructure.Services;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Builds an execution plan for a report: property paths, EF includes, and searchable fields.
/// </summary>
internal sealed record ReportQueryPlan(
    IReadOnlyList<string> PropertyPaths,
    IReadOnlyList<string> IncludePaths,
    IReadOnlyList<string> SearchablePaths);

internal static class ReportQueryPlanner
{
    public static ReportQueryPlan Create<T>(ForgeReport report, ReportQueryRequest request) where T : class
    {
        var paths = CollectPropertyPaths(report, request).ToList();
        var includes = ReportPropertyPathResolver.GetMinimalIncludePaths(paths);
        var searchable = paths
            .Where(p => ReportPropertyPathResolver.IsStringPath(typeof(T), p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReportQueryPlan(paths, includes, searchable);
    }

    public static IEnumerable<string> CollectPropertyPaths(ForgeReport report, ReportQueryRequest? request = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in ReportService.GetSourcePropertyColumns(report))
            paths.Add(name);

        foreach (var filter in report.Filters)
        {
            if (!string.IsNullOrWhiteSpace(filter.PropertyName))
                paths.Add(filter.PropertyName.Trim());
        }

        foreach (var group in report.Groups)
        {
            if (!string.IsNullOrWhiteSpace(group.PropertyName))
                paths.Add(group.PropertyName.Trim());
        }

        foreach (var summary in report.Summaries)
        {
            if (!string.IsNullOrWhiteSpace(summary.PropertyName))
                paths.Add(summary.PropertyName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request?.SortColumn))
            paths.Add(request.SortColumn.Trim());

        return paths;
    }
}
