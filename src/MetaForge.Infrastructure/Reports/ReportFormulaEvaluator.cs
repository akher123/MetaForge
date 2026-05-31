using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Evaluates report column formulas against materialized row dictionaries.
/// Formula syntax: <c>{PropertyName}</c> tokens with +, -, *, /, and parentheses.
/// Example: <c>{Quantity} * {UnitPrice}</c>
/// </summary>
internal static partial class ReportFormulaEvaluator
{
    private static readonly DataTable ComputeTable = new();

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled)]
    private static partial Regex FieldTokenRegex();

    public static HashSet<string> ExtractDependencies(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return [];

        return FieldTokenRegex()
            .Matches(formula)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static void ApplyCalculations(
        List<Dictionary<string, object?>> rows,
        IEnumerable<ReportColumnDefinitionDto> calculatedColumns)
    {
        var columns = calculatedColumns
            .Where(c => !string.IsNullOrWhiteSpace(c.Formula))
            .ToList();

        if (columns.Count == 0)
            return;

        foreach (var row in rows)
        {
            foreach (var column in columns)
                row[column.PropertyName] = Evaluate(column.Formula, row);
        }
    }

    public static object? Evaluate(string? formula, IReadOnlyDictionary<string, object?> row)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return null;

        var expression = FieldTokenRegex().Replace(formula.Trim(), match =>
        {
            var name = match.Groups[1].Value;
            if (!TryGetValue(row, name, out var value))
                return "0";

            return ToNumericLiteral(value);
        });

        if (string.IsNullOrWhiteSpace(expression))
            return null;

        try
        {
            var result = ComputeTable.Compute(expression, string.Empty);
            if (result is DBNull)
                return null;

            return Convert.ToDecimal(result, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> row, string name, out object? value)
    {
        if (row.TryGetValue(name, out value))
            return true;

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string ToNumericLiteral(object? value)
    {
        if (value == null)
            return "0";

        if (value is decimal d)
            return d.ToString(CultureInfo.InvariantCulture);

        if (value is int or long or short or byte)
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        if (value is double or float)
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed.ToString(CultureInfo.InvariantCulture);

        return "0";
    }
}
