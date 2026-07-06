using ERP.Application.Common.Interfaces;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Commands;

/// <summary>
/// Moves stock between two warehouses. Cost is preserved: units are issued from the source
/// (consuming FIFO layers) and received into the destination at the exact cost removed,
/// so the transfer neither creates nor destroys inventory value.
/// </summary>
public sealed record TransferStockCommand(
    Guid ProductId,
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    decimal Quantity,
    string? Reference,
    string? Notes) : IRequest<Result>;

public sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FromWarehouseId).NotEmpty();
        RuleFor(x => x.ToWarehouseId).NotEmpty();
        RuleFor(x => x.ToWarehouseId).NotEqual(x => x.FromWarehouseId)
            .WithMessage("Source and destination warehouses must differ.");
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class TransferStockCommandHandler : IRequestHandler<TransferStockCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public TransferStockCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(TransferStockCommand request, CancellationToken ct)
    {
        var source = await _db.InventoryItems
            .Include(i => i.Layers)
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.FromWarehouseId, ct);

        if (source is null || source.QuantityOnHand < request.Quantity)
            return Result.Failure(Error.Conflict(
                $"Insufficient stock at source: requested {request.Quantity}, available {source?.QuantityOnHand ?? 0}."));

        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.ToWarehouseId, ct))
            return Result.Failure(Error.Validation("Destination warehouse does not exist."));

        var now = _clock.UtcNow;

        // Remove from source at FIFO cost, then place into destination at that same cost
        // (weighted-average unit cost of the units moved) — value is conserved.
        var cogs = source.Issue(request.Quantity);
        var transferUnitCost = cogs / request.Quantity;

        var destination = await InventoryItemLoader.GetOrCreateAsync(_db, request.ProductId, request.ToWarehouseId, ct);
        destination.Receive(request.Quantity, transferUnitCost);

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.FromWarehouseId,
            Type = MovementType.TransferOut,
            Quantity = request.Quantity,
            TotalCost = cogs,
            OccurredAt = now,
            Reference = request.Reference,
            Notes = request.Notes
        });
        _db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.ToWarehouseId,
            Type = MovementType.TransferIn,
            Quantity = request.Quantity,
            UnitCost = transferUnitCost,
            TotalCost = cogs,
            OccurredAt = now,
            Reference = request.Reference,
            Notes = request.Notes
        });

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
