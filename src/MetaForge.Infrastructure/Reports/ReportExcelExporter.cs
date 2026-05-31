using System.Globalization;
using ClosedXML.Excel;

namespace MetaForge.Infrastructure.Reports;

internal static class ReportExcelExporter
{
    public static byte[] Export(ReportResultDto result, ReportExportLayoutDto layout)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(layout.Title));
        var columns = result.Columns.Where(c => c.IsVisible).ToList();
        var columnCount = Math.Max(columns.Count, 1);

        var rowIndex = 1;
        rowIndex = WriteHeaderBand(worksheet, layout, columnCount, rowIndex);
        rowIndex = WriteTitleBlock(worksheet, layout, columnCount, rowIndex);
        rowIndex++;

        var headerRow = rowIndex;
        WriteHeaderRow(worksheet, columns, headerRow);
        rowIndex++;

        foreach (var row in result.Rows)
        {
            WriteDataRow(worksheet, rowIndex, columns, row);
            rowIndex++;
        }

        if (result.GrandTotals.Count > 0
            && result.Rows.All(r => r.RowType != ReportRowTypes.GrandTotal))
        {
            WriteGrandTotalRow(worksheet, rowIndex, columns, result.GrandTotals);
            rowIndex++;
        }

        if (layout.ShowSignatureBlock && layout.Signatures.Count > 0)
            rowIndex = WriteSignatureBlock(worksheet, layout, columnCount, rowIndex + 2);

        if (ReportExportTokenFormatter.HasFooter(layout))
            WriteFooterBand(worksheet, layout, columnCount, rowIndex + 2);

        ApplyPrintHeaderFooter(worksheet, layout);

        worksheet.Columns(1, columnCount).AdjustToContents();
        worksheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int WriteHeaderBand(IXLWorksheet worksheet, ReportExportLayoutDto layout, int columnCount, int rowIndex)
    {
        if (!ReportExportTokenFormatter.HasHeader(layout))
            return rowIndex;

        var third = Math.Max(1, columnCount / 3);
        WriteBandCell(worksheet, rowIndex, 1, third, ReportExportTokenFormatter.Format(layout.HeaderLeft, layout), XLAlignmentHorizontalValues.Left);
        WriteBandCell(worksheet, rowIndex, third + 1, third * 2, ReportExportTokenFormatter.Format(layout.HeaderCenter, layout), XLAlignmentHorizontalValues.Center);
        WriteBandCell(worksheet, rowIndex, (third * 2) + 1, columnCount, ReportExportTokenFormatter.Format(layout.HeaderRight, layout), XLAlignmentHorizontalValues.Right);

        return rowIndex + 1;
    }

    private static void WriteFooterBand(IXLWorksheet worksheet, ReportExportLayoutDto layout, int columnCount, int rowIndex)
    {
        var third = Math.Max(1, columnCount / 3);
        WriteBandCell(worksheet, rowIndex, 1, third, ReportExportTokenFormatter.Format(layout.FooterLeft, layout), XLAlignmentHorizontalValues.Left);

        var centerText = ReportExportTokenFormatter.Format(layout.FooterCenter, layout);
        if (string.IsNullOrWhiteSpace(centerText) && layout.ShowPageNumbers)
            centerText = "Page {Page} of {Pages}";
        WriteBandCell(worksheet, rowIndex, third + 1, third * 2, centerText, XLAlignmentHorizontalValues.Center);

        var rightText = ReportExportTokenFormatter.Format(layout.FooterRight, layout);
        WriteBandCell(worksheet, rowIndex, (third * 2) + 1, columnCount, rightText, XLAlignmentHorizontalValues.Right);
    }

    private static void WriteBandCell(
        IXLWorksheet worksheet,
        int rowIndex,
        int startCol,
        int endCol,
        string text,
        XLAlignmentHorizontalValues alignment)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var range = worksheet.Range(rowIndex, startCol, rowIndex, endCol);
        range.Merge();
        range.FirstCell().Value = text;
        range.Style.Font.FontSize = 9;
        range.Style.Font.FontColor = XLColor.Gray;
        range.Style.Alignment.Horizontal = alignment;
    }

    private static void ApplyPrintHeaderFooter(IXLWorksheet worksheet, ReportExportLayoutDto layout)
    {
        if (ReportExportTokenFormatter.HasHeader(layout))
        {
            worksheet.PageSetup.Header.Left.AddText(ReportExportTokenFormatter.Format(layout.HeaderLeft, layout));
            worksheet.PageSetup.Header.Center.AddText(ReportExportTokenFormatter.Format(layout.HeaderCenter, layout));
            worksheet.PageSetup.Header.Right.AddText(ReportExportTokenFormatter.Format(layout.HeaderRight, layout));
        }

        worksheet.PageSetup.Footer.Left.AddText(ReportExportTokenFormatter.Format(layout.FooterLeft, layout));
        worksheet.PageSetup.Footer.Center.AddText(
            string.IsNullOrWhiteSpace(layout.FooterCenter) && layout.ShowPageNumbers
                ? "Page &P of &N"
                : ReportExportTokenFormatter.Format(layout.FooterCenter, layout));
        worksheet.PageSetup.Footer.Right.AddText(ReportExportTokenFormatter.Format(layout.FooterRight, layout));
    }

    private static int WriteTitleBlock(IXLWorksheet worksheet, ReportExportLayoutDto layout, int columnCount, int rowIndex)
    {
        var titleRange = worksheet.Range(rowIndex, 1, rowIndex, columnCount);
        titleRange.Merge();
        titleRange.FirstCell().Value = layout.Title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if (layout.ShowTitleUnderline)
            titleRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        rowIndex++;

        if (layout.ShowGeneratedTimestamp)
        {
            var subtitleRange = worksheet.Range(rowIndex, 1, rowIndex, columnCount);
            subtitleRange.Merge();
            subtitleRange.FirstCell().Value = ReportExportTokenFormatter.Format("Generated {DateTime}", layout);
            subtitleRange.Style.Font.FontColor = XLColor.Gray;
            subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rowIndex++;
        }

        return rowIndex;
    }

    private static int WriteSignatureBlock(
        IXLWorksheet worksheet,
        ReportExportLayoutDto layout,
        int columnCount,
        int startRow)
    {
        var signatures = layout.Signatures.OrderBy(s => s.DisplayOrder).ToList();
        var blockWidth = Math.Max(1, columnCount / signatures.Count);

        for (var index = 0; index < signatures.Count; index++)
        {
            var startCol = index * blockWidth + 1;
            var endCol = index == signatures.Count - 1 ? columnCount : Math.Min(columnCount, (index + 1) * blockWidth);
            var labelRow = startRow;
            var lineRow = startRow + 2;

            var labelRange = worksheet.Range(labelRow, startCol, labelRow, endCol);
            labelRange.Merge();
            labelRange.FirstCell().Value = signatures[index].Label;
            labelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            labelRange.Style.Font.Bold = true;

            var lineRange = worksheet.Range(lineRow, startCol, lineRow, endCol);
            lineRange.Merge();
            lineRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        return startRow + 3;
    }

    private static void WriteHeaderRow(IXLWorksheet worksheet, IReadOnlyList<ReportColumnDefinitionDto> columns, int rowIndex)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var cell = worksheet.Cell(rowIndex, i + 1);
            cell.Value = columns[i].Label;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void WriteDataRow(
        IXLWorksheet worksheet,
        int rowIndex,
        IReadOnlyList<ReportColumnDefinitionDto> columns,
        ReportRowDto row)
    {
        for (var col = 0; col < columns.Count; col++)
        {
            var property = columns[col].PropertyName;
            var cell = worksheet.Cell(rowIndex, col + 1);
            row.Values.TryGetValue(property, out var value);

            if (col == 0 && !string.IsNullOrWhiteSpace(row.Label)
                && (row.RowType is ReportRowTypes.GroupHeader or ReportRowTypes.GroupSubtotal or ReportRowTypes.GrandTotal or ReportRowTypes.Summary))
            {
                cell.Value = row.Label;
            }
            else
            {
                WriteCellValue(cell, value, columns[col].DisplayFormat);
            }

            ApplyRowStyle(cell, row, col);
        }
    }

    private static void WriteGrandTotalRow(
        IXLWorksheet worksheet,
        int rowIndex,
        IReadOnlyList<ReportColumnDefinitionDto> columns,
        Dictionary<string, object?> grandTotals)
    {
        for (var col = 0; col < columns.Count; col++)
        {
            var property = columns[col].PropertyName;
            var cell = worksheet.Cell(rowIndex, col + 1);

            if (col == 0)
                cell.Value = "Grand Total";
            else
            {
                grandTotals.TryGetValue(property, out var value);
                WriteCellValue(cell, value, columns[col].DisplayFormat);
            }

            cell.Style.Font.Bold = true;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Double;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
        }
    }

    private static void WriteCellValue(IXLCell cell, object? value, string? displayFormat)
    {
        if (value == null)
        {
            cell.Value = string.Empty;
            return;
        }

        if (value is decimal or int or long or double or float)
        {
            cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(displayFormat))
                cell.Style.NumberFormat.Format = displayFormat;
            return;
        }

        if (value is DateTime dt)
        {
            cell.Value = dt;
            cell.Style.NumberFormat.Format = string.IsNullOrWhiteSpace(displayFormat) ? "yyyy-MM-dd" : displayFormat;
            return;
        }

        cell.Value = value.ToString() ?? string.Empty;
    }

    private static void ApplyRowStyle(IXLCell cell, ReportRowDto row, int columnIndex)
    {
        switch (row.RowType)
        {
            case ReportRowTypes.GroupHeader:
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D1ECF1");
                if (columnIndex == 0)
                    cell.Style.Alignment.Indent = row.Level + 1;
                break;

            case ReportRowTypes.GroupSubtotal:
                cell.Style.Font.Bold = true;
                cell.Style.Font.Italic = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
                if (columnIndex == 0)
                    cell.Style.Alignment.Indent = row.Level + 1;
                break;

            case ReportRowTypes.GrandTotal:
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Double;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
                break;

            case ReportRowTypes.Summary:
                cell.Style.Font.Bold = false;
                break;

            default:
                if (columnIndex == 0 && row.Level > 0)
                    cell.Style.Alignment.Indent = row.Level + 1;
                break;
        }
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? ' ' : ch).ToArray()).Trim();
        return sanitized.Length > 31 ? sanitized[..31] : (string.IsNullOrWhiteSpace(sanitized) ? "Report" : sanitized);
    }
}
