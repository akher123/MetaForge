using System.Globalization;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Culture;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Replaces export layout tokens in header/footer text.
/// Supported: {Title}, {Date}, {DateTime}, {Page}, {Pages}
/// </summary>
internal static class ReportExportTokenFormatter
{
    public static string Format(string? template, ReportExportLayoutDto layout, int? page = null, int? totalPages = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var now = DateTime.Now;
        var dateFormat = DisplayFormatContext.Preferences?.DateFormat ?? GridDisplayFormats.LocaleDate;
        var dateTimeFormat = DisplayFormatContext.Preferences?.DateTimeFormat ?? GridDisplayFormats.LocaleDateTime;

        return template
            .Replace("{Title}", layout.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", GridDisplayFormats.FormatWithKey(now, dateFormat), StringComparison.OrdinalIgnoreCase)
            .Replace("{DateTime}", GridDisplayFormats.FormatWithKey(now, dateTimeFormat), StringComparison.OrdinalIgnoreCase)
            .Replace("{Page}", page?.ToString(CultureInfo.CurrentCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Pages}", totalPages?.ToString(CultureInfo.CurrentCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasHeader(ReportExportLayoutDto layout) =>
        !string.IsNullOrWhiteSpace(layout.HeaderLeft)
        || !string.IsNullOrWhiteSpace(layout.HeaderCenter)
        || !string.IsNullOrWhiteSpace(layout.HeaderRight);

    public static bool HasFooter(ReportExportLayoutDto layout) =>
        !string.IsNullOrWhiteSpace(layout.FooterLeft)
        || !string.IsNullOrWhiteSpace(layout.FooterCenter)
        || !string.IsNullOrWhiteSpace(layout.FooterRight)
        || layout.ShowPageNumbers;
}
