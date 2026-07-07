namespace ERP.Application.Features.Sales.Invoices;

/// <summary>A fully-resolved invoice ready to render as a Fiji-style tax invoice document.</summary>
public sealed record InvoiceDocument(
    // Seller (company profile)
    string SellerName,
    string? SellerTin,
    string? SellerAddress,
    string? SellerContact,
    // Buyer (customer)
    string BuyerName,
    string? BuyerTin,
    string? BuyerAddress,
    // Invoice
    string Number,
    string IssueDate,
    string? DueDate,
    string Status,
    string FiscalStatus,
    string? FiscalReference,
    string Currency,
    IReadOnlyList<InvoiceDocumentLine> Lines,
    decimal SubTotal,
    decimal TaxTotal,
    decimal Total,
    decimal AmountPaid,
    decimal Balance);

public sealed record InvoiceDocumentLine(
    string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate, decimal LineTax, decimal LineTotal);
