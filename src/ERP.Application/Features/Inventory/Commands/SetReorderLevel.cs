using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Commands;

/// <summary>Sets the reorder threshold for a product at a warehouse.</summary>
public sealed record SetReorderLevelCommand(Guid ProductId, Guid WarehouseId, decimal ReorderLevel)
    : IRequest<Result>;

public sealed class SetReorderLevelCommandValidator : AbstractValidator<SetReorderLevelCommand>
{
    public SetReorderLevelCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetReorderLevelCommandHandler : IRequestHandler<SetReorderLevelCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public SetReorderLevelCommandHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result> Handle(SetReorderLevelCommand request, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, ct))
            return Result.Failure(Error.Validation("Product does not exist."));
        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            return Result.Failure(Error.Validation("Warehouse does not exist."));
        if (!await _scope.CanAccessWarehouseAsync(request.WarehouseId, ct))
            return Result.Failure(Error.Unauthorized("Warehouse is outside your branch."));

        var item = await InventoryItemLoader.GetOrCreateAsync(_db, request.ProductId, request.WarehouseId, ct);
        item.ReorderLevel = request.ReorderLevel;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
