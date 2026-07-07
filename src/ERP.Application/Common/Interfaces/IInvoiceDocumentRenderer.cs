using ERP.Application.Features.Sales.Invoices;

namespace ERP.Application.Common.Interfaces;

/// <summary>Renders an <see cref="InvoiceDocument"/> to a PDF tax invoice. Implemented in Infrastructure (QuestPDF).</summary>
public interface IInvoiceDocumentRenderer
{
    byte[] RenderPdf(InvoiceDocument document);
}
