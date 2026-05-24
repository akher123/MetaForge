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

        var lookupMaps = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entityName in lookupColumns.Select(c => c.LookupEntity!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var items = await lookupService.GetLookupItemsAsync(entityName, null, null, cancellationToken);
            lookupMaps[entityName] = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Value))
                .GroupBy(i => i.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Text ?? g.Key, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var row in rows)
        {
            foreach (var column in lookupColumns)
            {
                if (!row.TryGetValue(column.PropertyName, out var rawValue) || rawValue == null)
                    continue;

                var key = Convert.ToString(rawValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                if (lookupMaps.TryGetValue(column.LookupEntity!, out var map)
                    && map.TryGetValue(key, out var displayText))
                {
                    row[column.PropertyName] = displayText;
                }
            }
        }
    }
}
