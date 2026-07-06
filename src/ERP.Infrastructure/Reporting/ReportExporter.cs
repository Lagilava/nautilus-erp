using System.Text;
using ClosedXML.Excel;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Reporting;

/// <summary>Renders report tables to CSV (native), Excel (ClosedXML), and PDF (QuestPDF).</summary>
public sealed class ReportExporter : IReportExporter
{
    static ReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public ExportedReport Export(ReportTable table, ReportFormat format) => format switch
    {
        ReportFormat.Csv => new ExportedReport(ToCsv(table), "text/csv", FileName(table, "csv")),
        ReportFormat.Excel => new ExportedReport(ToExcel(table),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(table, "xlsx")),
        ReportFormat.Pdf => new ExportedReport(ToPdf(table), "application/pdf", FileName(table, "pdf")),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string FileName(ReportTable table, string ext)
    {
        var slug = new string(table.Title.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        return $"{slug}-{DateTime.UtcNow:yyyyMMdd}.{ext}";
    }

    private static byte[] ToCsv(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Headers.Select(Escape)));
        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return Encoding.UTF8.GetBytes(sb.ToString());

        static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    private static byte[] ToExcel(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(table.Title, 31));

        for (var c = 0; c < table.Headers.Count; c++)
            sheet.Cell(1, c + 1).Value = table.Headers[c];
        sheet.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Count; c++)
                sheet.Cell(r + 2, c + 1).Value = table.Rows[r][c];

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] ToPdf(ReportTable table)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4.Landscape());
                page.Header().Text(table.Title).FontSize(16).SemiBold();
                page.Content().PaddingVertical(10).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in table.Headers) cols.RelativeColumn();
                    });
                    foreach (var header in table.Headers)
                        t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(header).SemiBold();
                    foreach (var row in table.Rows)
                        foreach (var cell in row)
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                });
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Generated ");
                    x.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            });
        });
        return document.GeneratePdf();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
