using ERP.Application.Common.Interfaces;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.SalesOrders;

public sealed record SalesOrderLineInput(Guid ProductId, decimal Quantity, decimal UnitPrice);

/// <summary>Creates a draft sales order with its lines.</summary>
public sealed record CreateSalesOrderCommand(
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly OrderDate,
    IReadOnlyList<SalesOrderLineInput> Lines,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateSalesOrderCommandValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("An order must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateSalesOrderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSalesOrderCommand request, CancellationToken ct)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
            return Result.Failure<Guid>(Error.Validation("Customer does not exist."));
        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            return Result.Failure<Guid>(Error.Validation("Warehouse does not exist."));

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var knownCount = await _db.Products.CountAsync(p => productIds.Contains(p.Id), ct);
        if (knownCount != productIds.Count)
            return Result.Failure<Guid>(Error.Validation("One or more products do not exist."));

        var sequence = await _db.SalesOrders.IgnoreQueryFilters().CountAsync(ct) + 1;
        var order = new SalesOrder
        {
            Number = DocumentNumber.For("SO", sequence),
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            OrderDate = request.OrderDate,
            Notes = request.Notes
        };

        foreach (var line in request.Lines)
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);

        _db.SalesOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return Result.Success(order.Id);
    }
}
