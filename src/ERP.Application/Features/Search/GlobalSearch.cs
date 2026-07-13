using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Search;

/// <summary>One hit in a global search: what it is, how to label it, and where to navigate.</summary>
public sealed record SearchHit(string Type, Guid Id, string Title, string? Subtitle);

/// <summary>
/// Searches the major entities (products, customers, suppliers, sales orders, invoices,
/// purchase orders, supplier invoices) by name/code/number in one round trip. Powers the
/// command palette, so each entity type is capped to a handful of hits — the palette shows
/// a shortlist, not a result page.
/// </summary>
public sealed record GlobalSearchQuery(string Term) : IRequest<Result<IReadOnlyList<SearchHit>>>;

public sealed class GlobalSearchQueryHandler
    : IRequestHandler<GlobalSearchQuery, Result<IReadOnlyList<SearchHit>>>
{
    private const int PerType = 5;

    private readonly IApplicationDbContext _db;

    public GlobalSearchQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<SearchHit>>> Handle(GlobalSearchQuery request, CancellationToken ct)
    {
        var term = request.Term?.Trim().ToLower() ?? string.Empty;
        if (term.Length < 2)
            return Result.Success<IReadOnlyList<SearchHit>>(Array.Empty<SearchHit>());

        var hits = new List<SearchHit>();

        hits.AddRange(await _db.Products.AsNoTracking()
            .Where(p => p.Sku.ToLower().Contains(term)
                        || p.Name.ToLower().Contains(term)
                        || (p.Barcode != null && p.Barcode.ToLower().Contains(term)))
            .OrderBy(p => p.Sku).Take(PerType)
            .Select(p => new SearchHit("product", p.Id, p.Name, p.Sku))
            .ToListAsync(ct));

        hits.AddRange(await _db.Customers.AsNoTracking()
            .Where(c => c.Code.ToLower().Contains(term)
                        || c.Name.ToLower().Contains(term)
                        || (c.Email != null && c.Email.ToLower().Contains(term)))
            .OrderBy(c => c.Code).Take(PerType)
            .Select(c => new SearchHit("customer", c.Id, c.Name, c.Code))
            .ToListAsync(ct));

        hits.AddRange(await _db.Suppliers.AsNoTracking()
            .Where(s => s.Code.ToLower().Contains(term)
                        || s.Name.ToLower().Contains(term)
                        || (s.Email != null && s.Email.ToLower().Contains(term)))
            .OrderBy(s => s.Code).Take(PerType)
            .Select(s => new SearchHit("supplier", s.Id, s.Name, s.Code))
            .ToListAsync(ct));

        hits.AddRange(await _db.SalesOrders.AsNoTracking()
            .Where(o => o.Number.ToLower().Contains(term))
            .OrderByDescending(o => o.Number).Take(PerType)
            .Join(_db.Customers, o => o.CustomerId, c => c.Id,
                (o, c) => new SearchHit("salesOrder", o.Id, o.Number, c.Name))
            .ToListAsync(ct));

        hits.AddRange(await _db.Invoices.AsNoTracking()
            .Where(i => i.Number.ToLower().Contains(term))
            .OrderByDescending(i => i.Number).Take(PerType)
            .Join(_db.Customers, i => i.CustomerId, c => c.Id,
                (i, c) => new SearchHit("invoice", i.Id, i.Number, c.Name))
            .ToListAsync(ct));

        hits.AddRange(await _db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Number.ToLower().Contains(term))
            .OrderByDescending(o => o.Number).Take(PerType)
            .Join(_db.Suppliers, o => o.SupplierId, s => s.Id,
                (o, s) => new SearchHit("purchaseOrder", o.Id, o.Number, s.Name))
            .ToListAsync(ct));

        hits.AddRange(await _db.SupplierInvoices.AsNoTracking()
            .Where(i => i.Number.ToLower().Contains(term)
                        || (i.SupplierReference != null && i.SupplierReference.ToLower().Contains(term)))
            .OrderByDescending(i => i.Number).Take(PerType)
            .Join(_db.Suppliers, i => i.SupplierId, s => s.Id,
                (i, s) => new SearchHit("supplierInvoice", i.Id, i.Number, s.Name))
            .ToListAsync(ct));

        return Result.Success<IReadOnlyList<SearchHit>>(hits);
    }
}
