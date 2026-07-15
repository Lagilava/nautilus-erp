using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.JournalEntries;

public sealed record JournalLineDto(Guid Id, Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Memo);

public sealed record JournalEntryDto(
    Guid Id, Guid? BranchId, DateOnly EntryDate, string Reference, string? Description,
    JournalEntryStatus Status, JournalEntrySource Source, Guid? SourceDocumentId,
    string? PreparedBy, string? PostedBy, decimal TotalDebits, decimal TotalCredits,
    IReadOnlyList<JournalLineDto> Lines);

public sealed record JournalEntrySummaryDto(
    Guid Id, DateOnly EntryDate, string Reference, JournalEntryStatus Status,
    JournalEntrySource Source, decimal TotalDebits, decimal TotalCredits);

public sealed record GetJournalEntryByIdQuery(Guid Id) : IRequest<Result<JournalEntryDto>>;

public sealed class GetJournalEntryByIdQueryHandler : IRequestHandler<GetJournalEntryByIdQuery, Result<JournalEntryDto>>
{
    private readonly IApplicationDbContext _db;
    public GetJournalEntryByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<JournalEntryDto>> Handle(GetJournalEntryByIdQuery request, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (entry is null) return Result.Failure<JournalEntryDto>(Error.NotFound("Journal entry not found."));

        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var lines = entry.Lines.Select(l => new JournalLineDto(
            l.Id, l.AccountId,
            accounts.TryGetValue(l.AccountId, out var acc) ? acc.Code : "?",
            accounts.TryGetValue(l.AccountId, out var acc2) ? acc2.Name : "(unknown)",
            l.Debit, l.Credit, l.Memo)).ToList();

        var dto = new JournalEntryDto(
            entry.Id, entry.BranchId, entry.EntryDate, entry.Reference, entry.Description,
            entry.Status, entry.Source, entry.SourceDocumentId, entry.PreparedBy, entry.PostedBy,
            entry.TotalDebits, entry.TotalCredits, lines);
        return Result.Success(dto);
    }
}

public sealed record GetJournalEntriesQuery : PagedQuery, IRequest<Result<PagedResult<JournalEntrySummaryDto>>>
{
    public JournalEntryStatus? Status { get; init; }
    public JournalEntrySource? Source { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed class GetJournalEntriesQueryHandler
    : IRequestHandler<GetJournalEntriesQuery, Result<PagedResult<JournalEntrySummaryDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetJournalEntriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<JournalEntrySummaryDto>>> Handle(GetJournalEntriesQuery request, CancellationToken ct)
    {
        var query = _db.JournalEntries.AsNoTracking().Include(e => e.Lines).AsQueryable();
        if (request.Status is { } st) query = query.Where(e => e.Status == st);
        if (request.Source is { } src) query = query.Where(e => e.Source == src);
        if (request.FromDate is { } from) query = query.Where(e => e.EntryDate >= from);
        if (request.ToDate is { } to) query = query.Where(e => e.EntryDate <= to);

        var total = await query.CountAsync(ct);
        var entries = await query
            .OrderByDescending(e => e.EntryDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = entries
            .Select(e => new JournalEntrySummaryDto(
                e.Id, e.EntryDate, e.Reference, e.Status, e.Source, e.TotalDebits, e.TotalCredits))
            .ToList();
        return Result.Success(new PagedResult<JournalEntrySummaryDto>(items, request.Page, request.PageSize, total));
    }
}
