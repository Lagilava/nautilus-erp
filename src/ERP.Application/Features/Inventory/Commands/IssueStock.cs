using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Commands;

/// <summary>Issues stock out of a warehouse, consuming FIFO layers and recording COGS.</summary>
public sealed record IssueStockCommand(
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    string? Reference,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class IssueStockCommandValidator : AbstractValidator<IssueStockCommand>
{
    public IssueStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reference).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class IssueStockCommandHandler : IRequestHandler<IssueStockCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;
    private readonly IBranchScope _scope;

    public IssueStockCommandHandler(IApplicationDbContext db, IDateTime clock, IBranchScope scope)
    {
        _db = db;
        _clock = clock;
        _scope = scope;
    }

    public async Task<Result<Guid>> Handle(IssueStockCommand request, CancellationToken ct)
    {
        if (!await _scope.CanAccessWarehouseAsync(request.WarehouseId, ct))
            return Result.Failure<Guid>(Error.Unauthorized("Warehouse is outside your branch."));

        var item = await _db.InventoryItems
            .Include(i => i.Layers)
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.WarehouseId, ct);

        if (item is null || item.QuantityOnHand < request.Quantity)
            return Result.Failure<Guid>(Error.Conflict(
                $"Insufficient stock: requested {request.Quantity}, available {item?.QuantityOnHand ?? 0}."));

        var cogs = item.Issue(request.Quantity);

        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Type = MovementType.Issue,
            Quantity = request.Quantity,
            TotalCost = cogs,
            OccurredAt = _clock.UtcNow,
            Reference = request.Reference,
            Notes = request.Notes
        };
        _db.StockMovements.Add(movement);

        await _db.SaveChangesAsync(ct);
        return Result.Success(movement.Id);
    }
}
