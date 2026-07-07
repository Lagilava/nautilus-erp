using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Features.Sales.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Reporting;

/// <summary>
/// Renders a Fiji-style TAX INVOICE: seller name + TIN, buyer name + TIN, line items with the
/// snapshotted VAT, totals in FJD, and the FRCS/VMS fiscalization status. Mirrors the fields a
/// compliant Fiji tax invoice must carry (per FRCS guidance) — the QR/verification block is
/// shown once a verified VMS adapter supplies a reference.
/// </summary>
public sealed class InvoiceDocumentRenderer : IInvoiceDocumentRenderer
{
    static InvoiceDocumentRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    private const string Ink = "#14312B";
    private const string Lagoon = "#0E7367";
    private const string Muted = "#697974";
    private const string Line = "#E7E3DA";

    public byte[] RenderPdf(InvoiceDocument d)
    {
        var money = (decimal v) => $"{d.Currency} {v.ToString("N2", CultureInfo.InvariantCulture)}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(d.SellerName).FontSize(16).Bold().FontColor(Lagoon);
                            if (!string.IsNullOrWhiteSpace(d.SellerAddress))
                                col.Item().Text(d.SellerAddress!).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(d.SellerContact))
                                col.Item().Text(d.SellerContact!).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(d.SellerTin))
                                col.Item().Text($"TIN: {d.SellerTin}").FontColor(Muted);
                        });
                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text("TAX INVOICE").FontSize(18).Bold();
                            col.Item().Text(d.Number).FontSize(12).FontColor(Muted);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1).LineColor(Line);
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    // Bill-to + meta
                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("BILL TO").FontSize(8).Bold().FontColor(Muted);
                            col.Item().Text(d.BuyerName).Bold();
                            if (!string.IsNullOrWhiteSpace(d.BuyerAddress))
                                col.Item().Text(d.BuyerAddress!).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(d.BuyerTin))
                                col.Item().Text($"TIN: {d.BuyerTin}").FontColor(Muted);
                        });
                        row.ConstantItem(200).Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Issue date").FontColor(Muted);
                                r.RelativeItem().AlignRight().Text(d.IssueDate);
                            });
                            if (!string.IsNullOrWhiteSpace(d.DueDate))
                                col.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Due date").FontColor(Muted);
                                    r.RelativeItem().AlignRight().Text(d.DueDate!);
                                });
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Status").FontColor(Muted);
                                r.RelativeItem().AlignRight().Text(d.Status);
                            });
                        });
                    });

                    // Line items
                    content.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.8f);
                        });

                        table.Header(h =>
                        {
                            static IContainer HeadCell(IContainer c) =>
                                c.PaddingVertical(5).BorderBottom(1).BorderColor(Lagoon);
                            HeadCell(h.Cell()).Text("Description").FontSize(8).Bold().FontColor(Muted);
                            HeadCell(h.Cell()).AlignRight().Text("Qty").FontSize(8).Bold().FontColor(Muted);
                            HeadCell(h.Cell()).AlignRight().Text("Unit").FontSize(8).Bold().FontColor(Muted);
                            HeadCell(h.Cell()).AlignRight().Text("VAT").FontSize(8).Bold().FontColor(Muted);
                            HeadCell(h.Cell()).AlignRight().Text("Amount").FontSize(8).Bold().FontColor(Muted);
                        });

                        foreach (var l in d.Lines)
                        {
                            static IContainer BodyCell(IContainer c) =>
                                c.PaddingVertical(5).BorderBottom(1).BorderColor(Line);
                            BodyCell(table.Cell()).Text(l.Description);
                            BodyCell(table.Cell()).AlignRight().Text(l.Quantity.ToString("0.####", CultureInfo.InvariantCulture));
                            BodyCell(table.Cell()).AlignRight().Text(money(l.UnitPrice));
                            BodyCell(table.Cell()).AlignRight().Text($"{l.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%");
                            BodyCell(table.Cell()).AlignRight().Text(money(l.LineTotal));
                        }
                    });

                    // Totals
                    content.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(240).Column(col =>
                        {
                            void Line2(string label, string value, bool strong = false)
                                => col.Item().PaddingVertical(2).Row(r =>
                                {
                                    r.RelativeItem().Text(label).FontColor(Muted);
                                    r.RelativeItem().AlignRight().Text(text =>
                                    {
                                        var span = text.Span(value);
                                        if (strong) span.Bold();
                                    });
                                });
                            Line2("Subtotal (excl. VAT)", money(d.SubTotal));
                            Line2("VAT", money(d.TaxTotal));
                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Line);
                            Line2("Total", money(d.Total), true);
                            Line2("Paid", money(d.AmountPaid));
                            Line2("Balance due", money(d.Balance), true);
                        });
                    });

                    // Fiscalization block (FRCS/VMS)
                    content.Item().PaddingTop(16).Background("#F7F5F0").Padding(10).Column(col =>
                    {
                        col.Item().Text("Fiscalization (FRCS VAT Monitoring System)").FontSize(8).Bold().FontColor(Muted);
                        if (d.FiscalStatus == "Submitted" && !string.IsNullOrWhiteSpace(d.FiscalReference))
                            col.Item().Text($"Fiscal reference: {d.FiscalReference}");
                        else
                            col.Item().Text(
                                "Not fiscalised — this document is not an FRCS-verified fiscal receipt. " +
                                "A verified VMS/SDC integration issues the fiscal number, digital signature and QR verification code.")
                                .FontColor(Muted);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated by Nautilus ERP · ").FontColor(Muted).FontSize(8);
                    t.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Muted).FontSize(8);
                });
            });
        }).GeneratePdf();
    }
}
