using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Invoices;

public sealed record InvoiceLineDto(
    Guid Id, Guid ProductId, string Description, decimal Quantity, decimal UnitPrice,
    decimal TaxRate, decimal LineSubTotal, decimal LineTax, decimal LineTotal);

public sealed record InvoiceDto(
    Guid Id, string Number, Guid CustomerId, Guid? SalesOrderId, DateOnly IssueDate, DateOnly? DueDate,
    InvoiceStatus Status, FiscalStatus FiscalStatus, string? FiscalReference,
    decimal SubTotal, decimal TaxTotal, decimal Total, decimal AmountPaid, decimal Balance,
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed record InvoiceSummaryDto(
    Guid Id, string Number, Guid CustomerId, DateOnly IssueDate, InvoiceStatus Status,
    FiscalStatus FiscalStatus, decimal Total, decimal Balance);

// ---- By id ----
public sealed record GetInvoiceByIdQuery(Guid Id) : IRequest<Result<InvoiceDto>>;

public sealed class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IApplicationDbContext _db;
    public GetInvoiceByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
            return Result.Failure<InvoiceDto>(Error.NotFound("Invoice not found."));

        var dto = new InvoiceDto(
            invoice.Id, invoice.Number, invoice.CustomerId, invoice.SalesOrderId,
            invoice.IssueDate, invoice.DueDate, invoice.Status, invoice.FiscalStatus, invoice.FiscalReference,
            invoice.SubTotal, invoice.TaxTotal, invoice.Total, invoice.AmountPaid, invoice.Balance,
            invoice.Lines.Select(l => new InvoiceLineDto(
                l.Id, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.TaxRate,
                l.LineSubTotal, l.LineTax, l.LineTotal)).ToList());

        return Result.Success(dto);
    }
}

// ---- List ----
public sealed record GetInvoicesQuery : PagedQuery, IRequest<Result<PagedResult<InvoiceSummaryDto>>>
{
    public Guid? CustomerId { get; init; }
    public InvoiceStatus? Status { get; init; }
}

public sealed class GetInvoicesQueryHandler
    : IRequestHandler<GetInvoicesQuery, Result<PagedResult<InvoiceSummaryDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetInvoicesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<InvoiceSummaryDto>>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var query = _db.Invoices.AsNoTracking().Include(i => i.Lines).AsQueryable();
        if (request.CustomerId is { } cid) query = query.Where(i => i.CustomerId == cid);
        if (request.Status is { } st) query = query.Where(i => i.Status == st);

        var total = await query.CountAsync(ct);
        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = invoices
            .Select(i => new InvoiceSummaryDto(
                i.Id, i.Number, i.CustomerId, i.IssueDate, i.Status, i.FiscalStatus, i.Total, i.Balance))
            .ToList();

        return Result.Success(new PagedResult<InvoiceSummaryDto>(items, request.Page, request.PageSize, total));
    }
}
