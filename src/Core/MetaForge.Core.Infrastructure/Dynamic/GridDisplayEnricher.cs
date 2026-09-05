using MetaForge.Application.DTOs;
using MetaForge.Shared.Constants;

namespace MetaForge.Infrastructure.Dynamic;

/// <summary>
/// Enriches grid rows: lookup ids to display text, and formats date/date-time columns.
/// </summary>
public static class GridDisplayEnricher
{
    public static async Task EnrichAsync(
        IList<Dictionary<string, object?>> rows,
        IReadOnlyList<GridColumnDefinition> columns,
        ILookupService lookupService,
        bool formatTemporalColumns = true,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            return;

        await EnrichLookupsAsync(rows, columns, lookupService, cancellationToken);

        if (formatTemporalColumns)
            FormatTemporalColumns(rows, columns);
    }

    private static async Task EnrichLookupsAsync(
        IList<Dictionary<string, object?>> rows,
        IReadOnlyList<GridColumnDefinition> columns,
        ILookupService lookupService,
        CancellationToken cancellationToken)
    {
        var lookupColumns = columns
            .Where(c => !string.IsNullOrWhiteSpace(c.LookupEntity))
            .ToList();

        if (lookupColumns.Count == 0)
            return;

        foreach (var column in lookupColumns)
        {
            var values = rows
                .Select(row => row.TryGetValue(column.PropertyName, out var rawValue) ? rawValue : null)
                .Where(v => v != null)
                .Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                .Where(v => v.Length > 0);

            var lookupTexts = await lookupService.ResolveLookupTextsAsync(
                column.LookupEntity!,
                values,
                cancellationToken);

            if (lookupTexts.Count == 0)
                continue;

            foreach (var row in rows)
            {
                if (!row.TryGetValue(column.PropertyName, out var rawValue) || rawValue == null)
                    continue;

                var key = Convert.ToString(rawValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                if (lookupTexts.TryGetValue(key, out var displayText))
                    row[column.PropertyName] = displayText;
            }
        }
    }

    private static void FormatTemporalColumns(
        IList<Dictionary<string, object?>> rows,
        IReadOnlyList<GridColumnDefinition> columns)
    {
        var temporalColumns = columns
            .Where(c => GridDisplayFormats.IsTemporalControlType(c.ControlType)
                || !string.IsNullOrWhiteSpace(c.DisplayFormat))
            .ToList();

        if (temporalColumns.Count == 0)
            return;

        foreach (var column in temporalColumns)
        {
            var formatKey = GridDisplayFormats.ResolveFormatKey(column.DisplayFormat, column.ControlType);
            if (string.IsNullOrEmpty(formatKey) && !GridDisplayFormats.IsTemporalControlType(column.ControlType))
                continue;

            foreach (var row in rows)
            {
                if (!row.TryGetValue(column.PropertyName, out var rawValue) || rawValue is null or "")
                    continue;

                row[column.PropertyName] = GridDisplayFormats.FormatValue(
                    rawValue,
                    column.DisplayFormat,
                    column.ControlType);
            }
        }
    }
}
