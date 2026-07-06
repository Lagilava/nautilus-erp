using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.SalesOrders;

public sealed record ConfirmSalesOrderCommand(Guid Id) : IRequest<Result>;
public sealed record CancelSalesOrderCommand(Guid Id) : IRequest<Result>;

public sealed class ConfirmSalesOrderCommandHandler : IRequestHandler<ConfirmSalesOrderCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public ConfirmSalesOrderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(ConfirmSalesOrderCommand request, CancellationToken ct)
    {
        var order = await _db.SalesOrders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (order is null) return Result.Failure(Error.NotFound("Sales order not found."));

        try { order.Confirm(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class CancelSalesOrderCommandHandler : IRequestHandler<CancelSalesOrderCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public CancelSalesOrderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(CancelSalesOrderCommand request, CancellationToken ct)
    {
        var order = await _db.SalesOrders.FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (order is null) return Result.Failure(Error.NotFound("Sales order not found."));

        try { order.Cancel(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
