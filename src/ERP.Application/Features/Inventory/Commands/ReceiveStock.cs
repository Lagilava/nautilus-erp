using ERP.Application.Common.Interfaces;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Commands;

/// <summary>Receives stock into a warehouse at a known unit cost (creates a FIFO layer).</summary>
public sealed record ReceiveStockCommand(
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    decimal UnitCost,
    string? Reference,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reference).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class ReceiveStockCommandHandler : IRequestHandler<ReceiveStockCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public ReceiveStockCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(ReceiveStockCommand request, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, ct))
            return Result.Failure<Guid>(Error.Validation("Product does not exist."));
        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            return Result.Failure<Guid>(Error.Validation("Warehouse does not exist."));

        var item = await InventoryItemLoader.GetOrCreateAsync(_db, request.ProductId, request.WarehouseId, ct);
        var totalCost = item.Receive(request.Quantity, request.UnitCost);

        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Type = MovementType.Receipt,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            TotalCost = totalCost,
            OccurredAt = _clock.UtcNow,
            Reference = request.Reference,
            Notes = request.Notes
        };
        _db.StockMovements.Add(movement);

        await _db.SaveChangesAsync(ct);
        return Result.Success(movement.Id);
    }
}
