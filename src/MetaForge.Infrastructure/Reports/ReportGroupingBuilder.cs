namespace MetaForge.Infrastructure.Reports;

internal static class ReportGroupingBuilder
{
    public static ReportBuildResult BuildGrouped(
        ForgeReport report,
        List<Dictionary<string, object?>> detailRows,
        List<ReportColumnDefinitionDto> detailColumns,
        List<ForgeReportColumn> aggregateColumns,
        List<ForgeReportSummary> summaries)
    {
        var groups = report.Groups.OrderBy(g => g.DisplayOrder).ToList();
        var rows = new List<ReportRowDto>();
        AppendGroupedRows(rows, detailRows, groups, 0, aggregateColumns);
        var grandTotals = ReportAggregateCalculator.ComputeSummaries(summaries, detailRows);

        if (grandTotals.Count > 0)
        {
            rows.Add(new ReportRowDto
            {
                RowType = ReportRowTypes.GrandTotal,
                Label = "Grand Total",
                Values = BuildGrandTotalValues(detailColumns, aggregateColumns, summaries, grandTotals)
            });
        }

        return new ReportBuildResult(rows, grandTotals, detailRows.Count);
    }

    public static ReportBuildResult BuildSummary(
        ForgeReport report,
        List<Dictionary<string, object?>> detailRows,
        List<ReportColumnDefinitionDto> displayColumns,
        List<ForgeReportColumn> aggregateColumns,
        List<ForgeReportSummary> summaries)
    {
        var groups = report.Groups.OrderBy(g => g.DisplayOrder).ToList();
        var rows = new List<ReportRowDto>();

        if (groups.Count == 0)
        {
            rows.Add(CreateSummaryRow("Total", detailRows, displayColumns, aggregateColumns, summaries));
        }
        else
        {
            AppendSummaryRows(rows, detailRows, groups, 0, displayColumns, aggregateColumns, summaries);
        }

        var grandTotals = ReportAggregateCalculator.ComputeSummaries(summaries, detailRows);
        if (grandTotals.Count > 0)
        {
            rows.Add(new ReportRowDto
            {
                RowType = ReportRowTypes.GrandTotal,
                Label = "Grand Total",
                Values = BuildGrandTotalValues(displayColumns, aggregateColumns, summaries, grandTotals)
            });
        }

        return new ReportBuildResult(rows, grandTotals, detailRows.Count);
    }

    private static void AppendGroupedRows(
        List<ReportRowDto> output,
        List<Dictionary<string, object?>> rows,
        IReadOnlyList<ForgeReportGroup> groups,
        int level,
        IReadOnlyList<ForgeReportColumn> aggregateColumns)
    {
        if (level >= groups.Count)
        {
            foreach (var row in rows)
            {
                output.Add(new ReportRowDto
                {
                    RowType = ReportRowTypes.Detail,
                    Level = level,
                    Values = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
                });
            }

            return;
        }

        var group = groups[level];
        var grouped = rows
            .GroupBy(r => NormalizeKey(r.GetValueOrDefault(group.PropertyName)))
            .OrderBy(g => g.Key, group.SortDescending ? Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase)) : StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in grouped)
        {
            var bucketRows = bucket.ToList();
            var displayValue = bucketRows.FirstOrDefault()?.GetValueOrDefault(group.PropertyName)?.ToString() ?? bucket.Key;

            if (group.ShowGroupHeader)
            {
                output.Add(new ReportRowDto
                {
                    RowType = ReportRowTypes.GroupHeader,
                    Level = level,
                    Label = $"{group.Label}: {displayValue}",
                    Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        [group.PropertyName] = displayValue
                    }
                });
            }

            AppendGroupedRows(output, bucketRows, groups, level + 1, aggregateColumns);

            if (group.ShowSubtotal && aggregateColumns.Count > 0)
            {
                var subtotalValues = ReportAggregateCalculator.ComputeAggregates(aggregateColumns, bucketRows);
                output.Add(new ReportRowDto
                {
                    RowType = ReportRowTypes.GroupSubtotal,
                    Level = level,
                    Label = $"Subtotal — {group.Label}: {displayValue}",
                    Values = subtotalValues
                });
            }
        }
    }

    private static void AppendSummaryRows(
        List<ReportRowDto> output,
        List<Dictionary<string, object?>> rows,
        IReadOnlyList<ForgeReportGroup> groups,
        int level,
        IReadOnlyList<ReportColumnDefinitionDto> displayColumns,
        IReadOnlyList<ForgeReportColumn> aggregateColumns,
        IReadOnlyList<ForgeReportSummary> summaries)
    {
        if (level >= groups.Count)
        {
            output.Add(CreateSummaryRow(null, rows, displayColumns, aggregateColumns, summaries));
            return;
        }

        var group = groups[level];
        var grouped = rows
            .GroupBy(r => NormalizeKey(r.GetValueOrDefault(group.PropertyName)))
            .OrderBy(g => g.Key, group.SortDescending ? Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase)) : StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in grouped)
        {
            var bucketRows = bucket.ToList();
            var displayValue = bucketRows.FirstOrDefault()?.GetValueOrDefault(group.PropertyName)?.ToString() ?? bucket.Key;
            var label = level < groups.Count - 1
                ? displayValue
                : $"{group.Label}: {displayValue}";

            if (level < groups.Count - 1)
            {
                AppendSummaryRows(output, bucketRows, groups, level + 1, displayColumns, aggregateColumns, summaries);
            }
            else
            {
                output.Add(CreateSummaryRow(label, bucketRows, displayColumns, aggregateColumns, summaries, group.PropertyName, displayValue));
            }
        }
    }

    private static ReportRowDto CreateSummaryRow(
        string? label,
        List<Dictionary<string, object?>> rows,
        IReadOnlyList<ReportColumnDefinitionDto> displayColumns,
        IReadOnlyList<ForgeReportColumn> aggregateColumns,
        IReadOnlyList<ForgeReportSummary> summaries,
        string? groupProperty = null,
        string? groupValue = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(groupProperty))
            values[groupProperty] = groupValue;

        foreach (var aggregate in aggregateColumns)
            values[aggregate.PropertyName] = ReportAggregateCalculator.Compute(aggregate.AggregateFunction, rows, aggregate.PropertyName);

        foreach (var summary in summaries)
        {
            if (!values.ContainsKey(summary.PropertyName))
                values[summary.PropertyName] = ReportAggregateCalculator.Compute(summary.AggregateFunction, rows, summary.PropertyName);
        }

        return new ReportRowDto
        {
            RowType = ReportRowTypes.Summary,
            Label = label,
            Values = values
        };
    }

    private static Dictionary<string, object?> BuildGrandTotalValues(
        IReadOnlyList<ReportColumnDefinitionDto> displayColumns,
        IReadOnlyList<ForgeReportColumn> aggregateColumns,
        IReadOnlyList<ForgeReportSummary> summaries,
        Dictionary<string, object?> grandTotals)
    {
        var values = new Dictionary<string, object?>(grandTotals, StringComparer.OrdinalIgnoreCase);

        if (displayColumns.Count > 0 && !values.ContainsKey(displayColumns[0].PropertyName))
            values[displayColumns[0].PropertyName] = "Grand Total";

        foreach (var aggregate in aggregateColumns)
        {
            if (!values.ContainsKey(aggregate.PropertyName))
                values[aggregate.PropertyName] = grandTotals.GetValueOrDefault(aggregate.PropertyName);
        }

        foreach (var summary in summaries)
            values[summary.PropertyName] = grandTotals.GetValueOrDefault(summary.PropertyName);

        return values;
    }

    private static string NormalizeKey(object? value) =>
        value?.ToString()?.Trim() ?? string.Empty;
}

internal sealed class ReportBuildResult(
    List<ReportRowDto> rows,
    Dictionary<string, object?> grandTotals,
    int detailCount)
{
    public List<ReportRowDto> Rows { get; } = rows;

    public Dictionary<string, object?> GrandTotals { get; } = grandTotals;

    public int DetailCount { get; } = detailCount;
}
