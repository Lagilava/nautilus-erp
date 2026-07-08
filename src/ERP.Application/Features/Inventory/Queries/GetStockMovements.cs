using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Inventory;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory.Queries;

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    Guid WarehouseId,
    MovementType Type,
    decimal Quantity,
    decimal? UnitCost,
    decimal TotalCost,
    DateTimeOffset OccurredAt,
    string? Reference,
    string? Notes);

/// <summary>Paged, filterable view of the immutable stock ledger (newest first).</summary>
public sealed record GetStockMovementsQuery : PagedQuery, IRequest<Result<PagedResult<StockMovementDto>>>
{
    public Guid? ProductId { get; init; }
    public Guid? WarehouseId { get; init; }
}

public sealed class GetStockMovementsQueryHandler
    : IRequestHandler<GetStockMovementsQuery, Result<PagedResult<StockMovementDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IBranchScope _scope;

    public GetStockMovementsQueryHandler(IApplicationDbContext db, IBranchScope scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<Result<PagedResult<StockMovementDto>>> Handle(GetStockMovementsQuery request, CancellationToken ct)
    {
        var query = _db.StockMovements.AsNoTracking();

        if (await _scope.AllowedWarehouseIdsAsync(ct) is { } allowed)
            query = query.Where(m => allowed.Contains(m.WarehouseId));

        if (request.ProductId is { } pid) query = query.Where(m => m.ProductId == pid);
        if (request.WarehouseId is { } wid) query = query.Where(m => m.WarehouseId == wid);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.OccurredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new StockMovementDto(
                m.Id, m.ProductId, m.WarehouseId, m.Type, m.Quantity,
                m.UnitCost, m.TotalCost, m.OccurredAt, m.Reference, m.Notes))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<StockMovementDto>(items, request.Page, request.PageSize, total));
    }
}
