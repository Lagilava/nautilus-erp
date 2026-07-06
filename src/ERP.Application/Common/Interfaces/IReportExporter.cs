using ERP.Application.Common.Reporting;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Renders a <see cref="ReportTable"/> to a downloadable file. Implemented in Infrastructure
/// (CSV natively, Excel via ClosedXML, PDF via QuestPDF).
/// </summary>
public interface IReportExporter
{
    ExportedReport Export(ReportTable table, ReportFormat format);
}
