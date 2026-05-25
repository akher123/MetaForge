namespace MetaForge.Infrastructure.Dynamic;

/// <summary>
/// Replaces foreign-key ids in grid rows with lookup display text.
/// </summary>
public static class GridDisplayEnricher
{
    public static async Task EnrichAsync(
        IList<Dictionary<string, object?>> rows,
        IReadOnlyList<GridColumnDefinition> columns,
        ILookupService lookupService,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            return;

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
}
