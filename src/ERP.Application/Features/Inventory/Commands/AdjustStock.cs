using ERP.Application.Common.Interfaces;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Commands;

/// <summary>
/// Corrects on-hand quantity from a stock-take. A positive delta adds stock at the given
/// unit cost (new FIFO layer); a negative delta removes stock at FIFO cost. Reason is
/// required for the audit trail.
/// </summary>
public sealed record AdjustStockCommand(
    Guid ProductId,
    Guid WarehouseId,
    decimal QuantityDelta,
    decimal? UnitCost,
    string Reason) : IRequest<Result<Guid>>;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.QuantityDelta).NotEqual(0).WithMessage("Adjustment quantity cannot be zero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.UnitCost)
            .NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.QuantityDelta > 0)
            .WithMessage("A positive adjustment requires a non-negative unit cost.");
    }
}

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public AdjustStockCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(AdjustStockCommand request, CancellationToken ct)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Layers)
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.WarehouseId, ct);

        decimal totalCost;
        MovementType type;

        if (request.QuantityDelta > 0)
        {
            if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, ct))
                return Result.Failure<Guid>(Error.Validation("Product does not exist."));
            if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
                return Result.Failure<Guid>(Error.Validation("Warehouse does not exist."));

            item ??= await InventoryItemLoader.GetOrCreateAsync(_db, request.ProductId, request.WarehouseId, ct);
            totalCost = item.Receive(request.QuantityDelta, request.UnitCost!.Value);
            type = MovementType.AdjustmentIn;
        }
        else
        {
            var removeQty = -request.QuantityDelta;
            if (item is null || item.QuantityOnHand < removeQty)
                return Result.Failure<Guid>(Error.Conflict(
                    $"Insufficient stock: requested {removeQty}, available {item?.QuantityOnHand ?? 0}."));

            totalCost = item.Issue(removeQty);
            type = MovementType.AdjustmentOut;
        }

        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Type = type,
            Quantity = Math.Abs(request.QuantityDelta),
            UnitCost = request.QuantityDelta > 0 ? request.UnitCost : null,
            TotalCost = totalCost,
            OccurredAt = _clock.UtcNow,
            Notes = request.Reason
        };
        _db.StockMovements.Add(movement);

        await _db.SaveChangesAsync(ct);
        return Result.Success(movement.Id);
    }
}
