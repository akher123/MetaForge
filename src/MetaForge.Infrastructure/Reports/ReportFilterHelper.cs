namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Normalizes report filter configuration and runtime submitted values.
/// </summary>
internal static class ReportFilterHelper
{
    public static string NormalizeControlType(string? controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
            return ReportFilterControlType.TextBox;

        return ReportFilterControlType.All.FirstOrDefault(c =>
            string.Equals(c, controlType, StringComparison.OrdinalIgnoreCase))
            ?? ReportFilterControlType.TextBox;
    }

    public static FilterOperator NormalizeOperator(string controlType, FilterOperator configured)
    {
        if (string.Equals(controlType, ReportFilterControlType.DateRange, StringComparison.OrdinalIgnoreCase))
            return FilterOperator.Between;

        return configured;
    }

    public static (string ControlType, FilterOperator Operator, string? LookupEntity, string? Options) InferForProperty(
        string propertyName,
        string? clrType,
        bool isForeignKey)
    {
        if (!string.IsNullOrWhiteSpace(clrType)
            && (clrType.Contains("DateTime", StringComparison.Ordinal)
                || clrType.Contains("DateOnly", StringComparison.Ordinal)
                || string.Equals(propertyName, "OrderDate", StringComparison.OrdinalIgnoreCase)))
        {
            return (ReportFilterControlType.DateRange, FilterOperator.Between, null, null);
        }

        if (string.Equals(propertyName, "Status", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith(".Status", StringComparison.OrdinalIgnoreCase))
        {
            return (ReportFilterControlType.Dropdown, FilterOperator.Equals, null, "Active,Inactive,Draft,Approved,Closed");
        }

        if (isForeignKey || (propertyName.EndsWith("Id", StringComparison.Ordinal) && !propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)))
        {
            var lookup = propertyName.Contains('.')
                ? propertyName.Split('.').Last()[..^2]
                : propertyName[..^2];
            return (ReportFilterControlType.Autocomplete, FilterOperator.Equals, lookup, null);
        }

        if (!string.IsNullOrWhiteSpace(clrType) && clrType.Contains("String", StringComparison.Ordinal))
            return (ReportFilterControlType.TextBox, FilterOperator.Contains, null, null);

        return (ReportFilterControlType.TextBox, FilterOperator.Equals, null, null);
    }

    public static IEnumerable<string> ParseOptions(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? []
            : options.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static void ApplyDateRangeValue(
        string propertyName,
        string rawValue,
        IDictionary<string, string> target)
    {
        var parts = rawValue.Split('|', 2, StringSplitOptions.TrimEntries);
        var from = parts.Length > 0 ? parts[0] : string.Empty;
        var to = parts.Length > 1 ? parts[1] : string.Empty;

        if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
        {
            target[$"{propertyName}__between"] = $"{from}|{to}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(from))
            target[$"{propertyName}__gte"] = from;

        if (!string.IsNullOrWhiteSpace(to))
            target[$"{propertyName}__lte"] = to;
    }
}
