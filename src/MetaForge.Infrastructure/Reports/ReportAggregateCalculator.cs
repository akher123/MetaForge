using System.Globalization;

namespace MetaForge.Infrastructure.Reports;

internal static class ReportAggregateCalculator
{
    public static object? Compute(ReportAggregateFunction function, IEnumerable<Dictionary<string, object?>> rows, string propertyName)
    {
        return function switch
        {
            ReportAggregateFunction.Count => rows.Count(),
            ReportAggregateFunction.Sum => Sum(rows, propertyName),
            ReportAggregateFunction.Avg => Average(rows, propertyName),
            ReportAggregateFunction.Min => Min(rows, propertyName),
            ReportAggregateFunction.Max => Max(rows, propertyName),
            _ => null
        };
    }

    public static Dictionary<string, object?> ComputeAggregates(
        IEnumerable<ForgeReportColumn> aggregateColumns,
        IEnumerable<Dictionary<string, object?>> rows)
    {
        var rowList = rows.ToList();
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in aggregateColumns)
        {
            if (column.AggregateFunction == ReportAggregateFunction.None)
                continue;

            values[column.PropertyName] = Compute(column.AggregateFunction, rowList, column.PropertyName);
        }

        return values;
    }

    public static Dictionary<string, object?> ComputeSummaries(
        IEnumerable<ForgeReportSummary> summaries,
        IEnumerable<Dictionary<string, object?>> rows)
    {
        var rowList = rows.ToList();
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var summary in summaries.OrderBy(s => s.DisplayOrder))
        {
            values[summary.PropertyName] = Compute(summary.AggregateFunction, rowList, summary.PropertyName);
        }

        return values;
    }

    private static decimal? Sum(IEnumerable<Dictionary<string, object?>> rows, string propertyName)
    {
        decimal total = 0;
        var hasValue = false;

        foreach (var value in rows.Select(r => ToDecimal(r.GetValueOrDefault(propertyName))))
        {
            if (!value.HasValue) continue;
            total += value.Value;
            hasValue = true;
        }

        return hasValue ? total : null;
    }

    private static decimal? Average(IEnumerable<Dictionary<string, object?>> rows, string propertyName)
    {
        decimal total = 0;
        var count = 0;

        foreach (var value in rows.Select(r => ToDecimal(r.GetValueOrDefault(propertyName))))
        {
            if (!value.HasValue) continue;
            total += value.Value;
            count++;
        }

        return count > 0 ? total / count : null;
    }

    private static object? Min(IEnumerable<Dictionary<string, object?>> rows, string propertyName)
    {
        decimal? min = null;
        foreach (var value in rows.Select(r => ToDecimal(r.GetValueOrDefault(propertyName))))
        {
            if (!value.HasValue) continue;
            min = min.HasValue ? Math.Min(min.Value, value.Value) : value.Value;
        }

        return min;
    }

    private static object? Max(IEnumerable<Dictionary<string, object?>> rows, string propertyName)
    {
        decimal? max = null;
        foreach (var value in rows.Select(r => ToDecimal(r.GetValueOrDefault(propertyName))))
        {
            if (!value.HasValue) continue;
            max = max.HasValue ? Math.Max(max.Value, value.Value) : value.Value;
        }

        return max;
    }

    private static decimal? ToDecimal(object? value)
    {
        if (value == null) return null;

        return value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            _ => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null
        };
    }
}
