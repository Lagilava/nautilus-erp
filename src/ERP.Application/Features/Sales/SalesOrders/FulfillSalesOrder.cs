using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.SalesOrders;

/// <summary>
/// Fulfils a confirmed order: issues each line's stock from the order's warehouse (FIFO,
/// recording COGS) and marks the order Fulfilled. This is the seam between Sales and
/// Inventory. All-or-nothing: if any line lacks stock, nothing is issued.
/// </summary>
public sealed record FulfillSalesOrderCommand(Guid Id) : IRequest<Result>;

public sealed class FulfillSalesOrderCommandHandler : IRequestHandler<FulfillSalesOrderCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public FulfillSalesOrderCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(FulfillSalesOrderCommand request, CancellationToken ct)
    {
        var order = await _db.SalesOrders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (order is null) return Result.Failure(Error.NotFound("Sales order not found."));

        // Aggregate demand per product (an order may list a product on multiple lines).
        var demand = order.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var items = await _db.InventoryItems.Include(i => i.Layers)
            .Where(i => i.WarehouseId == order.WarehouseId && demand.Keys.Contains(i.ProductId))
            .ToListAsync(ct);
        var itemsByProduct = items.ToDictionary(i => i.ProductId);

        // Pre-check availability so we don't partially issue before failing.
        foreach (var (productId, qty) in demand)
        {
            var available = itemsByProduct.TryGetValue(productId, out var it) ? it.QuantityOnHand : 0m;
            if (available < qty)
                return Result.Failure(Error.Conflict(
                    $"Insufficient stock for product {productId}: need {qty}, have {available}."));
        }

        try { order.MarkFulfilled(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        var now = _clock.UtcNow;
        foreach (var (productId, qty) in demand)
        {
            var item = itemsByProduct[productId];
            var cogs = item.Issue(qty);
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                WarehouseId = order.WarehouseId,
                Type = MovementType.Issue,
                Quantity = qty,
                TotalCost = cogs,
                OccurredAt = now,
                Reference = order.Number,
                Notes = "Sales order fulfilment"
            });
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
