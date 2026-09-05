using MetaForge.Application.DTOs;

namespace MetaForge.Application.Common;

/// <summary>
/// Parses tree level display column configuration (comma-separated property names).
/// </summary>
public static class TreeDisplayColumnParser
{
    public static List<string> ParseProperties(string? displayColumn)
    {
        if (string.IsNullOrWhiteSpace(displayColumn))
            return ["Name"];

        return displayColumn
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<TreeDisplayColumnDto> BuildColumns(
        string? displayColumn,
        IEnumerable<GridColumnDefinition>? gridColumns = null)
    {
        var grid = gridColumns?.ToList() ?? [];
        return ParseProperties(displayColumn).Select(property =>
        {
            var match = grid.FirstOrDefault(c =>
                string.Equals(c.PropertyName, property, StringComparison.OrdinalIgnoreCase));

            return new TreeDisplayColumnDto
            {
                PropertyName = property,
                Label = string.IsNullOrWhiteSpace(match?.Label) ? property : match!.Label
            };
        }).ToList();
    }

    public static List<TreeDisplayColumnDto> BuildColumns(
        string? displayColumn,
        IEnumerable<FormGridColumnConfigDto>? gridColumns)
    {
        var mapped = gridColumns?.Select(c => new GridColumnDefinition
        {
            PropertyName = c.PropertyName,
            Label = c.Label
        });

        return BuildColumns(displayColumn, mapped);
    }

    public static string BuildLabel(IReadOnlyDictionary<string, object?> row, IEnumerable<string> displayColumns, int fallbackId)
    {
        var parts = displayColumns
            .Select(column => ResolveValue(row, column))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return parts.Count > 0 ? string.Join(" · ", parts) : fallbackId.ToString();
    }

    public static string? ResolveValue(IReadOnlyDictionary<string, object?> row, string propertyName)
    {
        if (row.TryGetValue(propertyName, out var exact))
            return exact?.ToString();

        var match = row.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase));

        return match.Value?.ToString();
    }
}
