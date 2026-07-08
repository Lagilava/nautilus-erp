using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Queries;

public sealed record StockLevelDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    bool IsBelowReorder,
    decimal StockValue);

/// <summary>Paged stock levels with FIFO valuation, optionally filtered to one warehouse or low stock.</summary>
public sealed record GetStockLevelsQuery : PagedQuery, IRequest<Result<PagedResult<StockLevelDto>>>
{
    public Guid? WarehouseId { get; init; }
    public bool LowStockOnly { get; init; }
}

public sealed class GetStockLevelsQueryHandler
    : IRequestHandler<GetStockLevelsQuery, Result<PagedResult<StockLevelDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public GetStockLevelsQueryHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<PagedResult<StockLevelDto>>> Handle(GetStockLevelsQuery request, CancellationToken ct)
    {
        var query = _db.InventoryItems.AsNoTracking();

        // Record-level security: a branch-scoped user only sees their branch's warehouses.
        if (await _scope.AllowedWarehouseIdsAsync(ct) is { } allowed)
            query = query.Where(i => allowed.Contains(i.WarehouseId));

        if (request.WarehouseId is { } wh)
            query = query.Where(i => i.WarehouseId == wh);
        if (request.LowStockOnly)
            query = query.Where(i => i.QuantityOnHand <= i.ReorderLevel);

        var total = await query.CountAsync(ct);

        // Join to product/warehouse for display names — translatable and avoids correlated
        // scalar subqueries. Stock value is the sum over remaining FIFO layers.
        //
        // Order by SKU, not QuantityOnHand: SQLite cannot ORDER BY a decimal, and paging needs a
        // stable server-side sort. Callers wanting "what needs replenishing" use lowStockOnly.
        var items = await query
            .Join(_db.Products, i => i.ProductId, p => p.Id, (i, p) => new { i, p })
            .Join(_db.Warehouses, x => x.i.WarehouseId, w => w.Id, (x, w) => new { x.i, x.p, w })
            .OrderBy(x => x.p.Sku)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new StockLevelDto(
                x.i.ProductId,
                x.p.Sku,
                x.p.Name,
                x.i.WarehouseId,
                x.w.Name,
                x.i.QuantityOnHand,
                x.i.ReorderLevel,
                x.i.QuantityOnHand <= x.i.ReorderLevel,
                x.i.Layers.Sum(l => l.RemainingQuantity * l.UnitCost)))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<StockLevelDto>(items, request.Page, request.PageSize, total));
    }
}
