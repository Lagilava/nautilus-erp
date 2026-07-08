using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Application.Features.Sales;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.PurchaseOrders;

public sealed record PurchaseOrderLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);

/// <summary>Creates a draft purchase order with its lines.</summary>
public sealed record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly OrderDate,
    IReadOnlyList<PurchaseOrderLineInput> Lines,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase order must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public CreatePurchaseOrderCommandHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
            return Result.Failure<Guid>(Error.Validation("Supplier does not exist."));
        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            return Result.Failure<Guid>(Error.Validation("Warehouse does not exist."));
        if (!await _scope.CanAccessWarehouseAsync(request.WarehouseId, ct))
            return Result.Failure<Guid>(Error.Unauthorized("Warehouse is outside your branch."));

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var knownCount = await _db.Products.CountAsync(p => productIds.Contains(p.Id), ct);
        if (knownCount != productIds.Count)
            return Result.Failure<Guid>(Error.Validation("One or more products do not exist."));

        var sequence = await _db.PurchaseOrders.IgnoreQueryFilters().CountAsync(ct) + 1;
        var order = new PurchaseOrder
        {
            Number = DocumentNumber.For("PO", sequence),
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            OrderDate = request.OrderDate,
            Notes = request.Notes
        };

        foreach (var line in request.Lines)
            order.AddLine(line.ProductId, line.Quantity, line.UnitCost);

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return Result.Success(order.Id);
    }
}
