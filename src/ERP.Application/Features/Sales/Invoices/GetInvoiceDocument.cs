using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Invoices;

/// <summary>Assembles the seller, buyer, and invoice data needed to render a tax invoice.</summary>
public sealed record GetInvoiceDocumentQuery(Guid Id) : IRequest<Result<InvoiceDocument>>;

public sealed class GetInvoiceDocumentQueryHandler : IRequestHandler<GetInvoiceDocumentQuery, Result<InvoiceDocument>>
{
    private readonly IApplicationDbContext _db;
    public GetInvoiceDocumentQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<InvoiceDocument>> Handle(GetInvoiceDocumentQuery request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
            return Result.Failure<InvoiceDocument>(Error.NotFound("Invoice not found."));

        var company = await _db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == invoice.CustomerId)
            .Select(c => new { c.Name, c.TaxIdentificationNumber, c.AddressLine1, c.City, c.Country })
            .FirstOrDefaultAsync(ct);

        static string? JoinAddress(params string?[] parts) =>
            string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } s ? s : null;

        var doc = new InvoiceDocument(
            SellerName: company?.LegalName ?? "Your Company Ltd",
            SellerTin: company?.Tin,
            SellerAddress: JoinAddress(company?.AddressLine1, company?.City, company?.Country),
            SellerContact: JoinAddress(company?.Phone, company?.Email),
            BuyerName: customer?.Name ?? "—",
            BuyerTin: customer?.TaxIdentificationNumber,
            BuyerAddress: JoinAddress(customer?.AddressLine1, customer?.City, customer?.Country),
            Number: invoice.Number,
            IssueDate: invoice.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            DueDate: invoice.DueDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            Status: invoice.Status.ToString(),
            FiscalStatus: invoice.FiscalStatus.ToString(),
            FiscalReference: invoice.FiscalReference,
            Currency: company?.BaseCurrency ?? "FJD",
            Lines: invoice.Lines.Select(l => new InvoiceDocumentLine(
                l.Description, l.Quantity, l.UnitPrice, l.TaxRate, l.LineTax, l.LineTotal)).ToList(),
            SubTotal: invoice.SubTotal,
            TaxTotal: invoice.TaxTotal,
            Total: invoice.Total,
            AmountPaid: invoice.AmountPaid,
            Balance: invoice.Balance);

        return Result.Success(doc);
    }
}
