using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.PurchaseOrders;

// ---- Transitions ----
public sealed record ConfirmPurchaseOrderCommand(Guid Id) : IRequest<Result>;
public sealed record CancelPurchaseOrderCommand(Guid Id) : IRequest<Result>;

public sealed class ConfirmPurchaseOrderCommandHandler : IRequestHandler<ConfirmPurchaseOrderCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ISegregationOfDuties _sod;
    private readonly ICurrentUserService _currentUser;
    private readonly IBranchScope _scope;

    public ConfirmPurchaseOrderCommandHandler(
        IApplicationDbContext db, ISegregationOfDuties sod, ICurrentUserService currentUser, IBranchScope scope)
    {
        _db = db;
        _sod = sod;
        _currentUser = currentUser;
        _scope = scope;
    }

    public async Task<Result> Handle(ConfirmPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (order is null || !await _scope.CanAccessWarehouseAsync(order.WarehouseId, ct))
            return Result.Failure(Error.NotFound("Purchase order not found."));

        // Maker-checker: the person who raised the order may not approve it.
        var sod = _sod.Ensure(SoDRule.PurchaseOrderApproval,
            "You cannot approve a purchase order you raised. It must be approved by someone else.",
            order.CreatedBy);
        if (sod.IsFailure) return sod;

        try { order.Confirm(_currentUser.UserId?.ToString()); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public CancelPurchaseOrderCommandHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result> Handle(CancelPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == request.Id, ct);

        // Cancellation is destructive and needs the same branch guard as approval; without it a
        // manager could halt another branch's procurement.
        if (order is null || !await _scope.CanAccessWarehouseAsync(order.WarehouseId, ct))
            return Result.Failure(Error.NotFound("Purchase order not found."));

        try { order.Cancel(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Queries ----
public sealed record PurchaseOrderLineDto(
    Guid Id, Guid ProductId, decimal Quantity, decimal UnitCost, decimal QuantityReceived,
    decimal OutstandingQuantity, decimal LineTotal);

public sealed record PurchaseOrderDto(
    Guid Id, string Number, Guid SupplierId, Guid WarehouseId, DateOnly OrderDate,
    PurchaseOrderStatus Status, decimal SubTotal, string? Notes, IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderSummaryDto(
    Guid Id, string Number, Guid SupplierId, DateOnly OrderDate, PurchaseOrderStatus Status, decimal SubTotal);

public sealed record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<Result<PurchaseOrderDto>>;

public sealed class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(GetPurchaseOrderByIdQuery request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct);

        // The lines carry supplier cost prices. A GUID is not a secret — it appears in audit
        // logs, stock-movement references and URLs shared between staff — so scope the
        // single-record read exactly as the list query below does. NotFound, not Forbidden:
        // a 403 would confirm the record exists.
        if (order is null || !await _scope.CanAccessWarehouseAsync(order.WarehouseId, ct))
            return Result.Failure<PurchaseOrderDto>(Error.NotFound("Purchase order not found."));

        var dto = new PurchaseOrderDto(
            order.Id, order.Number, order.SupplierId, order.WarehouseId, order.OrderDate,
            order.Status, order.SubTotal, order.Notes,
            order.Lines.Select(l => new PurchaseOrderLineDto(
                l.Id, l.ProductId, l.Quantity, l.UnitCost, l.QuantityReceived, l.OutstandingQuantity, l.LineTotal)).ToList());
        return Result.Success(dto);
    }
}

public sealed record GetPurchaseOrdersQuery : PagedQuery, IRequest<Result<PagedResult<PurchaseOrderSummaryDto>>>
{
    public Guid? SupplierId { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
}

public sealed class GetPurchaseOrdersQueryHandler
    : IRequestHandler<GetPurchaseOrdersQuery, Result<PagedResult<PurchaseOrderSummaryDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<PagedResult<PurchaseOrderSummaryDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var query = _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).AsQueryable();

        // Purchase orders belong to a branch through their receiving warehouse.
        if (await _scope.AllowedWarehouseIdsAsync(ct) is { } allowed)
            query = query.Where(o => allowed.Contains(o.WarehouseId));

        if (request.SupplierId is { } sid) query = query.Where(o => o.SupplierId == sid);
        if (request.Status is { } st) query = query.Where(o => o.Status == st);

        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = orders
            .Select(o => new PurchaseOrderSummaryDto(o.Id, o.Number, o.SupplierId, o.OrderDate, o.Status, o.SubTotal))
            .ToList();
        return Result.Success(new PagedResult<PurchaseOrderSummaryDto>(items, request.Page, request.PageSize, total));
    }
}
