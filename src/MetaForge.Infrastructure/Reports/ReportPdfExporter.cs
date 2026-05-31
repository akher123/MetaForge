using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MetaForge.Infrastructure.Reports;

internal static class ReportPdfExporter
{
    static ReportPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Export(ReportResultDto result, ReportExportLayoutDto layout)
    {
        var columns = result.Columns.Where(c => c.IsVisible).ToList();

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8));

                if (ReportExportTokenFormatter.HasHeader(layout))
                {
                    page.Header().Height(28).Element(container => ComposeHeader(container, layout));
                }

                page.Content().Column(column =>
                {
                    column.Item().Element(container => ComposeTitle(container, layout));
                    column.Item().PaddingTop(8).Table(table => ComposeTable(table, columns, result.Rows));

                    if (layout.ShowSignatureBlock && layout.Signatures.Count > 0)
                        column.Item().PaddingTop(24).Element(container => ComposeSignatures(container, layout));
                });

                if (ReportExportTokenFormatter.HasFooter(layout))
                {
                    page.Footer().Height(24).Element(container => ComposeFooter(container, layout));
                }
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportExportLayoutDto layout)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text(ReportExportTokenFormatter.Format(layout.HeaderLeft, layout)).FontSize(8);
            row.RelativeItem().AlignCenter().Text(ReportExportTokenFormatter.Format(layout.HeaderCenter, layout)).FontSize(8);
            row.RelativeItem().AlignRight().Text(ReportExportTokenFormatter.Format(layout.HeaderRight, layout)).FontSize(8);
        });
    }

    private static void ComposeFooter(IContainer container, ReportExportLayoutDto layout)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text(text =>
            {
                text.Span(ReportExportTokenFormatter.Format(layout.FooterLeft, layout)).FontSize(8);
            });

            row.RelativeItem().AlignCenter().Text(text =>
            {
                var center = ReportExportTokenFormatter.Format(layout.FooterCenter, layout);
                if (!string.IsNullOrWhiteSpace(center))
                    text.Span(center).FontSize(8);
                else if (layout.ShowPageNumbers)
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                }
            });

            row.RelativeItem().AlignRight().Text(text =>
            {
                var right = ReportExportTokenFormatter.Format(layout.FooterRight, layout);
                if (!string.IsNullOrWhiteSpace(right))
                {
                    text.Span(right).FontSize(8);
                    return;
                }

                if (layout.ShowPageNumbers && !string.IsNullOrWhiteSpace(layout.FooterCenter))
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                }
            });
        });
    }

    private static void ComposeTitle(IContainer container, ReportExportLayoutDto layout)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text(layout.Title).Bold().FontSize(16);

            if (layout.ShowTitleUnderline)
                column.Item().PaddingTop(4).LineHorizontal(1.5f);

            if (layout.ShowGeneratedTimestamp)
            {
                column.Item().PaddingTop(6).AlignCenter().Text(
                        ReportExportTokenFormatter.Format("Generated {DateTime}", layout))
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeTable(TableDescriptor table, IReadOnlyList<ReportColumnDefinitionDto> columns, List<ReportRowDto> rows)
    {
        table.ColumnsDefinition(definition =>
        {
            foreach (var _ in columns)
                definition.RelativeColumn();
        });

        table.Header(header =>
        {
            foreach (var column in columns)
            {
                header.Cell()
                    .Background(Colors.Grey.Lighten2)
                    .BorderBottom(0.5f)
                    .Padding(4)
                    .Text(column.Label)
                    .SemiBold();
            }
        });

        foreach (var row in rows)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                var text = FormatCellText(row, column, index);
                table.Cell()
                    .Element(cell => ApplyRowStyle(cell, row.RowType))
                    .Padding(3)
                    .Text(text);
            }
        }
    }

    private static void ComposeSignatures(IContainer container, ReportExportLayoutDto layout)
    {
        var signatures = layout.Signatures.OrderBy(s => s.DisplayOrder).ToList();

        container.Row(row =>
        {
            foreach (var signature in signatures)
            {
                row.RelativeItem().PaddingHorizontal(8).Column(column =>
                {
                    column.Item().AlignCenter().Text(signature.Label).SemiBold().FontSize(9);
                    column.Item().PaddingTop(28).LineHorizontal(0.75f);
                });
            }
        });
    }

    private static string FormatCellText(ReportRowDto row, ReportColumnDefinitionDto column, int columnIndex)
    {
        if (columnIndex == 0
            && !string.IsNullOrWhiteSpace(row.Label)
            && row.RowType is ReportRowTypes.GroupHeader or ReportRowTypes.GroupSubtotal or ReportRowTypes.GrandTotal or ReportRowTypes.Summary)
        {
            return row.Label;
        }

        row.Values.TryGetValue(column.PropertyName, out var value);
        return FormatValue(value, column.DisplayFormat);
    }

    private static string FormatValue(object? value, string? displayFormat)
    {
        if (value == null)
            return string.Empty;

        if (value is decimal dec)
            return string.IsNullOrWhiteSpace(displayFormat)
                ? dec.ToString("N2", CultureInfo.CurrentCulture)
                : dec.ToString(displayFormat, CultureInfo.CurrentCulture);

        if (value is int or long or double or float)
        {
            var numeric = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(displayFormat)
                ? numeric.ToString(CultureInfo.CurrentCulture)
                : numeric.ToString(displayFormat, CultureInfo.CurrentCulture);
        }

        if (value is DateTime dt)
            return string.IsNullOrWhiteSpace(displayFormat)
                ? dt.ToString("d", CultureInfo.CurrentCulture)
                : dt.ToString(displayFormat, CultureInfo.CurrentCulture);

        return value.ToString() ?? string.Empty;
    }

    private static IContainer ApplyRowStyle(IContainer container, string rowType) =>
        rowType switch
        {
            ReportRowTypes.GroupHeader => container.Background(Colors.Cyan.Lighten4).DefaultTextStyle(style => style.SemiBold()),
            ReportRowTypes.GroupSubtotal => container.Background(Colors.Grey.Lighten3).DefaultTextStyle(style => style.Italic().SemiBold()),
            ReportRowTypes.GrandTotal => container.Background(Colors.Yellow.Lighten4).DefaultTextStyle(style => style.Bold()),
            ReportRowTypes.Summary => container.Background(Colors.Grey.Lighten4),
            _ => container
        };
}
