using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Application.Features.Sales;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.PurchaseOrders;

public sealed record ReorderDraftResult(Guid PurchaseOrderId, string Number, int LineCount);

/// <summary>
/// One-click replenishment: creates a draft purchase order covering every product in the
/// warehouse that is at or below its reorder level. Quantities top the item back up to twice
/// its reorder level (level = safety stock, so landing exactly on it would trigger again
/// immediately); unit costs default to the product's cost price. The order stays a draft —
/// a buyer reviews, edits, and confirms it through the normal PO lifecycle.
/// </summary>
public sealed record CreateReorderDraftCommand(Guid SupplierId, Guid WarehouseId) : IRequest<Result<ReorderDraftResult>>;

public sealed class CreateReorderDraftCommandValidator : AbstractValidator<CreateReorderDraftCommand>
{
    public CreateReorderDraftCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}

public sealed class CreateReorderDraftCommandHandler
    : IRequestHandler<CreateReorderDraftCommand, Result<ReorderDraftResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public CreateReorderDraftCommandHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<ReorderDraftResult>> Handle(CreateReorderDraftCommand request, CancellationToken ct)
    {
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.IsActive, ct))
            return Result.Failure<ReorderDraftResult>(Error.Validation("Supplier does not exist or is inactive."));
        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            return Result.Failure<ReorderDraftResult>(Error.Validation("Warehouse does not exist."));
        if (!await _scope.CanAccessWarehouseAsync(request.WarehouseId, ct))
            return Result.Failure<ReorderDraftResult>(Error.Unauthorized("Warehouse is outside your branch."));

        // ReorderLevel > 0: items with no reorder level set have opted out of replenishment.
        var lowStock = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.WarehouseId == request.WarehouseId
                        && i.ReorderLevel > 0
                        && i.QuantityOnHand <= i.ReorderLevel)
            .Join(_db.Products.Where(p => p.IsActive), i => i.ProductId, p => p.Id,
                (i, p) => new { p.Id, p.Sku, i.QuantityOnHand, i.ReorderLevel, p.CostPrice })
            .OrderBy(x => x.Sku)
            .ToListAsync(ct);

        if (lowStock.Count == 0)
            return Result.Failure<ReorderDraftResult>(
                Error.Validation("Nothing to reorder: no products in this warehouse are at or below their reorder level."));

        var sequence = await _db.PurchaseOrders.IgnoreQueryFilters().CountAsync(ct) + 1;
        var order = new PurchaseOrder
        {
            Number = DocumentNumber.For("PO", sequence),
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Notes = "Auto-generated from low-stock reorder suggestions."
        };

        foreach (var item in lowStock)
            order.AddLine(item.Id, item.ReorderLevel * 2 - item.QuantityOnHand, item.CostPrice);

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return Result.Success(new ReorderDraftResult(order.Id, order.Number, order.Lines.Count));
    }
}
