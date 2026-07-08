using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.SalesOrders;

public sealed record SalesOrderLineDto(Guid Id, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record SalesOrderDto(
    Guid Id, string Number, Guid CustomerId, Guid WarehouseId, DateOnly OrderDate,
    SalesOrderStatus Status, decimal SubTotal, string? Notes, IReadOnlyList<SalesOrderLineDto> Lines);

public sealed record SalesOrderSummaryDto(
    Guid Id, string Number, Guid CustomerId, DateOnly OrderDate, SalesOrderStatus Status, decimal SubTotal);

// ---- By id ----
public sealed record GetSalesOrderByIdQuery(Guid Id) : IRequest<Result<SalesOrderDto>>;

public sealed class GetSalesOrderByIdQueryHandler : IRequestHandler<GetSalesOrderByIdQuery, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _db;
    public GetSalesOrderByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<SalesOrderDto>> Handle(GetSalesOrderByIdQuery request, CancellationToken ct)
    {
        var order = await _db.SalesOrders.AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);

        if (order is null)
            return Result.Failure<SalesOrderDto>(Error.NotFound("Sales order not found."));

        var dto = new SalesOrderDto(
            order.Id, order.Number, order.CustomerId, order.WarehouseId, order.OrderDate,
            order.Status, order.SubTotal, order.Notes,
            order.Lines.Select(l => new SalesOrderLineDto(l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());

        return Result.Success(dto);
    }
}

// ---- List ----
public sealed record GetSalesOrdersQuery : PagedQuery, IRequest<Result<PagedResult<SalesOrderSummaryDto>>>
{
    public Guid? CustomerId { get; init; }
    public SalesOrderStatus? Status { get; init; }
}

public sealed class GetSalesOrdersQueryHandler
    : IRequestHandler<GetSalesOrdersQuery, Result<PagedResult<SalesOrderSummaryDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public GetSalesOrdersQueryHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<PagedResult<SalesOrderSummaryDto>>> Handle(GetSalesOrdersQuery request, CancellationToken ct)
    {
        var query = _db.SalesOrders.AsNoTracking().Include(o => o.Lines).AsQueryable();

        // Orders belong to a branch through their fulfilment warehouse.
        if (await _scope.AllowedWarehouseIdsAsync(ct) is { } allowed)
            query = query.Where(o => allowed.Contains(o.WarehouseId));

        if (request.CustomerId is { } cid) query = query.Where(o => o.CustomerId == cid);
        if (request.Status is { } st) query = query.Where(o => o.Status == st);

        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = orders
            .Select(o => new SalesOrderSummaryDto(o.Id, o.Number, o.CustomerId, o.OrderDate, o.Status, o.SubTotal))
            .ToList();

        return Result.Success(new PagedResult<SalesOrderSummaryDto>(items, request.Page, request.PageSize, total));
    }
}
