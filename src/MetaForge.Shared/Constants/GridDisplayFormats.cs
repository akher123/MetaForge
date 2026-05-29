namespace MetaForge.Shared.Constants;

/// <summary>
/// Preset display format keys for list/grid date and date-time columns.
/// Empty or null means infer from the field control type.
/// </summary>
public static class GridDisplayFormats
{
    public const string Auto = "";

    public const string DateShort = "date-short";
    public const string DateIso = "date-iso";
    public const string DateLong = "date-long";

    public const string DateTimeShort = "datetime-short";
    public const string DateTimeFull = "datetime-full";
    public const string DateTimeIso = "datetime-iso";

    public const string LocaleDate = "locale-date";
    public const string LocaleDateTime = "locale-datetime";

    public static readonly IReadOnlyList<(string Key, string Label, string Group)> Presets =
    [
        (Auto, "Default (from field type)", "General"),
        (DateShort, "dd/MM/yyyy", "Date"),
        (DateIso, "yyyy-MM-dd", "Date"),
        (DateLong, "dd MMM yyyy", "Date"),
        (LocaleDate, "Locale date", "Date"),
        (DateTimeShort, "dd/MM/yyyy HH:mm", "Date & time"),
        (DateTimeFull, "dd/MM/yyyy HH:mm:ss", "Date & time"),
        (DateTimeIso, "yyyy-MM-dd HH:mm", "Date & time"),
        (LocaleDateTime, "Locale date & time", "Date & time"),
    ];

    public const string ControlTypeDate = "Date";
    public const string ControlTypeDateTime = "DateTime";

    public static string? GetDefaultForControlType(string? controlType)
    {
        if (string.Equals(controlType, ControlTypeDate, StringComparison.OrdinalIgnoreCase))
            return DateShort;

        if (string.Equals(controlType, ControlTypeDateTime, StringComparison.OrdinalIgnoreCase))
            return DateTimeShort;

        return null;
    }

    public static bool IsTemporalControlType(string? controlType) =>
        string.Equals(controlType, ControlTypeDate, StringComparison.OrdinalIgnoreCase)
        || string.Equals(controlType, ControlTypeDateTime, StringComparison.OrdinalIgnoreCase);

    public static string FormatValue(object? value, string? displayFormat, string? controlType)
    {
        if (value is null or "")
            return string.Empty;

        if (!TryParseDateTime(value, out var dt))
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

        var formatKey = ResolveFormatKey(displayFormat, controlType);
        if (string.IsNullOrEmpty(formatKey))
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

        return ApplyPreset(dt, formatKey);
    }

    public static string ResolveFormatKey(string? displayFormat, string? controlType)
    {
        if (!string.IsNullOrWhiteSpace(displayFormat))
            return displayFormat.Trim();

        return GetDefaultForControlType(controlType) ?? string.Empty;
    }

    private static bool TryParseDateTime(object value, out DateTime dt)
    {
        switch (value)
        {
            case DateTime dateTime:
                dt = dateTime;
                return true;
            case DateTimeOffset dto:
                dt = dto.DateTime;
                return true;
            case DateOnly dateOnly:
                dt = dateOnly.ToDateTime(TimeOnly.MinValue);
                return true;
        }

        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            dt = default;
            return false;
        }

        if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out dt))
            return true;

        if (DateTime.TryParse(text, out dt))
            return true;

        dt = default;
        return false;
    }

    private static string ApplyPreset(DateTime dt, string formatKey)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        return formatKey switch
        {
            DateShort => dt.ToString("dd/MM/yyyy", culture),
            DateIso => dt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            DateLong => dt.ToString("dd MMM yyyy", culture),
            DateTimeShort => dt.ToString("dd/MM/yyyy HH:mm", culture),
            DateTimeFull => dt.ToString("dd/MM/yyyy HH:mm:ss", culture),
            DateTimeIso => dt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            LocaleDate => dt.ToString("d", culture),
            LocaleDateTime => dt.ToString("g", culture),
            _ when formatKey.Contains('y', StringComparison.Ordinal)
                || formatKey.Contains('d', StringComparison.Ordinal)
                || formatKey.Contains('H', StringComparison.Ordinal)
                || formatKey.Contains('m', StringComparison.Ordinal) =>
                dt.ToString(formatKey, culture),
            _ => dt.ToString(culture)
        };
    }
}
