using System.Globalization;
using MetaForge.Shared.Constants;

namespace MetaForge.Shared.Culture;

/// <summary>
/// Culture-aware display samples for UI previews (dates, numbers).
/// </summary>
public static class LocaleFormatting
{
    private static readonly DateTime SampleDateTime = new(2026, 7, 15, 14, 30, 0, DateTimeKind.Unspecified);

    public static string FormatShortDate(CultureInfo culture, string? formatKey = null) =>
        GridDisplayFormats.FormatWithKey(SampleDateTime, formatKey ?? GridDisplayFormats.LocaleDate, culture);

    public static string FormatShortDateTime(CultureInfo culture, string? formatKey = null) =>
        GridDisplayFormats.FormatWithKey(SampleDateTime, formatKey ?? GridDisplayFormats.LocaleDateTime, culture);

    public static string FormatSampleNumber(CultureInfo culture) =>
        1234567.89m.ToString("N2", culture);

    public static CulturePreviewDto BuildPreview(string cultureName, string? dateFormat = null, string? dateTimeFormat = null)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var effectiveDateFormat = dateFormat ?? DisplayFormatContext.Preferences?.DateFormat ?? GridDisplayFormats.LocaleDate;
        var effectiveDateTimeFormat = dateTimeFormat ?? DisplayFormatContext.Preferences?.DateTimeFormat ?? GridDisplayFormats.LocaleDateTime;

        return new CulturePreviewDto
        {
            Culture = culture.Name,
            ShortDate = FormatShortDate(culture, effectiveDateFormat),
            ShortDateTime = FormatShortDateTime(culture, effectiveDateTimeFormat),
            SampleNumber = FormatSampleNumber(culture),
            IsRtl = culture.TextInfo.IsRightToLeft,
            DateFormat = effectiveDateFormat,
            DateTimeFormat = effectiveDateTimeFormat
        };
    }
}

public sealed class CulturePreviewDto
{
    public required string Culture { get; init; }

    public required string ShortDate { get; init; }

    public required string ShortDateTime { get; init; }

    public required string SampleNumber { get; init; }

    public bool IsRtl { get; init; }

    public string? DateFormat { get; init; }

    public string? DateTimeFormat { get; init; }
}
