using System.Globalization;
using MetaForge.Shared.Constants;

namespace MetaForge.Shared.Culture;

/// <summary>
/// Culture-aware date and date-time format options for system and user preferences.
/// </summary>
public static class DateFormatCatalog
{
    private static readonly DateTime SampleDateTime = new(2026, 7, 15, 14, 30, 0, DateTimeKind.Unspecified);

    public static IReadOnlyList<DateFormatOptionDto> GetDateOptions(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var options = new List<DateFormatOptionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string label, string pattern, bool invariant = false)
        {
            if (!seen.Add(key))
                return;

            var sample = invariant
                ? SampleDateTime.ToString(pattern, CultureInfo.InvariantCulture)
                : SampleDateTime.ToString(pattern, culture);

            options.Add(new DateFormatOptionDto
            {
                Key = key,
                Label = label,
                Sample = sample,
                Group = "Date"
            });
        }

        Add(GridDisplayFormats.LocaleDate, "Locale short", "d");
        Add(GridDisplayFormats.LocaleLongDate, "Locale long", "D");
        Add(GridDisplayFormats.DateIso, "ISO", "yyyy-MM-dd", invariant: true);

        var shortPattern = culture.DateTimeFormat.ShortDatePattern;
        if (!string.IsNullOrWhiteSpace(shortPattern))
            Add($"{GridDisplayFormats.PatternPrefix}{shortPattern}", $"Short ({shortPattern})", shortPattern);

        var longPattern = culture.DateTimeFormat.LongDatePattern;
        if (!string.IsNullOrWhiteSpace(longPattern)
            && !string.Equals(longPattern, shortPattern, StringComparison.Ordinal))
        {
            Add($"{GridDisplayFormats.PatternPrefix}{longPattern}", $"Long ({longPattern})", longPattern);
        }

        Add(GridDisplayFormats.DateShort, "dd/MM/yyyy", "dd/MM/yyyy");
        Add(GridDisplayFormats.DateUs, "MM/dd/yyyy", "MM/dd/yyyy");
        Add(GridDisplayFormats.DateLong, "dd MMM yyyy", "dd MMM yyyy");

        return options;
    }

    public static IReadOnlyList<DateFormatOptionDto> GetDateTimeOptions(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var options = new List<DateFormatOptionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string label, string pattern, bool invariant = false)
        {
            if (!seen.Add(key))
                return;

            var sample = invariant
                ? SampleDateTime.ToString(pattern, CultureInfo.InvariantCulture)
                : SampleDateTime.ToString(pattern, culture);

            options.Add(new DateFormatOptionDto
            {
                Key = key,
                Label = label,
                Sample = sample,
                Group = "Date & time"
            });
        }

        Add(GridDisplayFormats.LocaleDateTime, "Locale short", "g");
        Add(GridDisplayFormats.LocaleLongDateTime, "Locale long", "G");
        Add(GridDisplayFormats.DateTimeIso, "ISO", "yyyy-MM-dd HH:mm", invariant: true);

        var shortPattern = $"{culture.DateTimeFormat.ShortDatePattern} {culture.DateTimeFormat.ShortTimePattern}";
        if (!string.IsNullOrWhiteSpace(shortPattern))
            Add($"{GridDisplayFormats.PatternPrefix}{shortPattern}", $"Short ({shortPattern})", shortPattern);

        var longPattern = culture.DateTimeFormat.FullDateTimePattern;
        if (!string.IsNullOrWhiteSpace(longPattern)
            && !string.Equals(longPattern, shortPattern, StringComparison.Ordinal))
        {
            Add($"{GridDisplayFormats.PatternPrefix}{longPattern}", $"Full ({longPattern})", longPattern);
        }

        Add(GridDisplayFormats.DateTimeShort, "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm");
        Add(GridDisplayFormats.DateTimeFull, "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss");

        return options;
    }

    public static bool IsValidDateFormat(string? formatKey, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(formatKey))
            return false;

        return GetDateOptions(cultureName).Any(o => string.Equals(o.Key, formatKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidDateTimeFormat(string? formatKey, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(formatKey))
            return false;

        return GetDateTimeOptions(cultureName).Any(o => string.Equals(o.Key, formatKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeDateFormat(string? formatKey, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(formatKey))
            return GridDisplayFormats.LocaleDate;

        var trimmed = formatKey.Trim();
        return IsValidDateFormat(trimmed, cultureName) ? trimmed : GridDisplayFormats.LocaleDate;
    }

    public static string NormalizeDateTimeFormat(string? formatKey, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(formatKey))
            return GridDisplayFormats.LocaleDateTime;

        var trimmed = formatKey.Trim();
        return IsValidDateTimeFormat(trimmed, cultureName) ? trimmed : GridDisplayFormats.LocaleDateTime;
    }

    public static string FormatSample(string formatKey, string cultureName, bool dateTime = false)
    {
        var options = dateTime ? GetDateTimeOptions(cultureName) : GetDateOptions(cultureName);
        return options.FirstOrDefault(o => string.Equals(o.Key, formatKey, StringComparison.OrdinalIgnoreCase))?.Sample
            ?? SampleDateTime.ToString(dateTime ? "g" : "d", CultureInfo.GetCultureInfo(cultureName));
    }
}

public sealed class DateFormatOptionDto
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Sample { get; init; }

    public required string Group { get; init; }
}
