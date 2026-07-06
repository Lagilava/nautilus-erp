using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Catalog.Products.Queries;

/// <summary>Paged, searchable list of products (search matches SKU, name, or barcode).</summary>
public sealed record GetProductsQuery : PagedQuery, IRequest<Result<PagedResult<ProductDto>>>;

public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<ProductDto>>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p =>
                p.Sku.Contains(term) || p.Name.Contains(term) ||
                (p.Barcode != null && p.Barcode.Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductDto(
                p.Id, p.Sku, p.Name, p.Description, p.Barcode,
                p.CategoryId, p.Category!.Name,
                p.UnitOfMeasureId, p.UnitOfMeasure!.Code,
                p.TaxId, p.Tax!.Code,
                p.CostPrice, p.SellingPrice, p.IsActive))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProductDto>(items, request.Page, request.PageSize, total));
    }
}
