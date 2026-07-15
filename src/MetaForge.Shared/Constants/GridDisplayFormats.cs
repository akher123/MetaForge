using System.Globalization;
using MetaForge.Shared.Culture;

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
    public const string LocaleLongDate = "locale-long";
    public const string LocaleDateTime = "locale-datetime";
    public const string LocaleLongDateTime = "locale-long-datetime";
    public const string DateUs = "date-us";
    public const string PatternPrefix = "pattern:";

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
            return LocaleDate;

        if (string.Equals(controlType, ControlTypeDateTime, StringComparison.OrdinalIgnoreCase))
            return LocaleDateTime;

        return null;
    }

    /// <summary>
    /// Maps legacy .NET patterns and empty values to preset keys.
    /// Empty/null resolves to locale-date/datetime which follows system preference.
    /// </summary>
    public static string? NormalizeFormatKey(string? displayFormat, string? controlType)
    {
        if (string.IsNullOrWhiteSpace(displayFormat))
            return GetDefaultForControlType(controlType);

        return displayFormat.Trim() switch
        {
            "d" => LocaleDate,
            "D" => LocaleLongDate,
            "g" => LocaleDateTime,
            "G" => LocaleLongDateTime,
            _ => displayFormat.Trim()
        };
    }

    public static bool UsesSystemDatePreference(string? displayFormat, string? controlType)
    {
        var key = NormalizeFormatKey(displayFormat, controlType);
        return string.Equals(key, LocaleDate, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, LocaleDateTime, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// List grid columns always follow global user/system date preferences.
    /// Per-column date format overrides from form builder are ignored.
    /// </summary>
    public static string? ResolveGridColumnDisplayFormat(string? controlType) =>
        GetDefaultForControlType(controlType);

    public static bool IsTemporalControlType(string? controlType) =>
        string.Equals(controlType, ControlTypeDate, StringComparison.OrdinalIgnoreCase)
        || string.Equals(controlType, ControlTypeDateTime, StringComparison.OrdinalIgnoreCase);

    public static bool IsTemporalDisplayFormat(string? displayFormat) =>
        !string.IsNullOrWhiteSpace(displayFormat)
        && (displayFormat.StartsWith("date", StringComparison.OrdinalIgnoreCase)
            || displayFormat.StartsWith("datetime", StringComparison.OrdinalIgnoreCase)
            || displayFormat.StartsWith("locale", StringComparison.OrdinalIgnoreCase)
            || displayFormat.StartsWith(PatternPrefix, StringComparison.OrdinalIgnoreCase)
            || displayFormat is "d" or "D" or "g" or "G");

    public static IReadOnlyList<DateFormatOptionDto> GetSelectOptions(string culture, string? controlType)
    {
        var options = new List<DateFormatOptionDto>
        {
            new()
            {
                Key = Auto,
                Label = "Default (from field type)",
                Sample = string.Empty,
                Group = "General"
            }
        };

        if (string.Equals(controlType, ControlTypeDateTime, StringComparison.OrdinalIgnoreCase))
            options.AddRange(DateFormatCatalog.GetDateTimeOptions(culture));
        else if (string.Equals(controlType, ControlTypeDate, StringComparison.OrdinalIgnoreCase))
            options.AddRange(DateFormatCatalog.GetDateOptions(culture));
        else
        {
            options.AddRange(DateFormatCatalog.GetDateOptions(culture));
            options.AddRange(DateFormatCatalog.GetDateTimeOptions(culture));
        }

        return options;
    }

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

    public static string FormatWithKey(DateTime dt, string formatKey, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return ApplyPreset(dt, formatKey, culture);
    }

    public static string ResolveEffectiveDateFormat(string? formatKey)
    {
        var normalized = NormalizeFormatKey(formatKey, ControlTypeDate) ?? LocaleDate;
        if (string.Equals(normalized, LocaleDate, StringComparison.OrdinalIgnoreCase))
            return DisplayFormatContext.Preferences?.DateFormat ?? LocaleDate;

        return normalized;
    }

    public static string ResolveEffectiveDateTimeFormat(string? formatKey)
    {
        var normalized = NormalizeFormatKey(formatKey, ControlTypeDateTime) ?? LocaleDateTime;
        if (string.Equals(normalized, LocaleDateTime, StringComparison.OrdinalIgnoreCase))
            return DisplayFormatContext.Preferences?.DateTimeFormat ?? LocaleDateTime;

        return normalized;
    }

    public static string ResolveFormatKey(string? displayFormat, string? controlType) =>
        NormalizeFormatKey(displayFormat, controlType) ?? string.Empty;

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

    private static string ApplyPreset(DateTime dt, string formatKey, CultureInfo? culture = null)
    {
        culture ??= System.Globalization.CultureInfo.CurrentCulture;

        if (string.Equals(formatKey, LocaleDate, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ResolveEffectiveDateFormat(formatKey);
            if (string.Equals(resolved, LocaleDate, StringComparison.OrdinalIgnoreCase))
                return dt.ToString("d", culture);
            return ApplyPreset(dt, resolved, culture);
        }

        if (string.Equals(formatKey, LocaleDateTime, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ResolveEffectiveDateTimeFormat(formatKey);
            if (string.Equals(resolved, LocaleDateTime, StringComparison.OrdinalIgnoreCase))
                return dt.ToString("g", culture);
            return ApplyPreset(dt, resolved, culture);
        }

        if (formatKey.StartsWith(PatternPrefix, StringComparison.OrdinalIgnoreCase))
            return dt.ToString(formatKey[PatternPrefix.Length..], culture);

        return formatKey switch
        {
            DateShort => dt.ToString("dd/MM/yyyy", culture),
            DateIso => dt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            DateLong => dt.ToString("dd MMM yyyy", culture),
            DateUs => dt.ToString("MM/dd/yyyy", culture),
            DateTimeShort => dt.ToString("dd/MM/yyyy HH:mm", culture),
            DateTimeFull => dt.ToString("dd/MM/yyyy HH:mm:ss", culture),
            DateTimeIso => dt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            LocaleLongDate => dt.ToString("D", culture),
            LocaleLongDateTime => dt.ToString("G", culture),
            _ when formatKey.Contains('y', StringComparison.Ordinal)
                || formatKey.Contains('d', StringComparison.Ordinal)
                || formatKey.Contains('H', StringComparison.Ordinal)
                || formatKey.Contains('m', StringComparison.Ordinal) =>
                dt.ToString(formatKey, culture),
            _ => dt.ToString(culture)
        };
    }
}
