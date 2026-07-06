namespace ERP.Application.Common.Reporting;

/// <summary>
/// A provider-agnostic tabular report: a title, column headers, and string-formatted rows.
/// Queries produce this; the <see cref="IReportExporter"/> renders it to CSV/Excel/PDF.
/// </summary>
public sealed record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Supported export formats.</summary>
public enum ReportFormat
{
    Csv = 1,
    Excel = 2,
    Pdf = 3
}

/// <summary>A rendered report ready to return as a file download.</summary>
public sealed record ExportedReport(byte[] Content, string ContentType, string FileName);
