using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Auditing;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Auditing;

public sealed record AuditLogDto(
    Guid Id, string EntityName, string EntityId, AuditAction Action,
    string? Changes, string? UserId, DateTimeOffset Timestamp);

/// <summary>Paged, filterable view of the audit trail (newest first).</summary>
public sealed record GetAuditLogsQuery : PagedQuery, IRequest<Result<PagedResult<AuditLogDto>>>
{
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
}

public sealed class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetAuditLogsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.EntityName))
            query = query.Where(a => a.EntityName == request.EntityName);
        if (!string.IsNullOrWhiteSpace(request.EntityId))
            query = query.Where(a => a.EntityId == request.EntityId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.EntityName, a.EntityId, a.Action, a.Changes, a.UserId, a.Timestamp))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AuditLogDto>(items, request.Page, request.PageSize, total));
    }
}
