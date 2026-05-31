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
        return template
            .Replace("{Title}", layout.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{DateTime}", now.ToString("yyyy-MM-dd HH:mm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Page}", page?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Pages}", totalPages?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
