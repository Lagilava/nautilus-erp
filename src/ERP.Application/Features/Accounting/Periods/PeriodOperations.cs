using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Domain.Common;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.Periods;

public sealed record AccountingPeriodDto(
    Guid Id, int Year, int Month, bool IsClosed, string? ClosedBy, DateTimeOffset? ClosedAt);

// ---- List periods (creates any missing period on the fly is out of scope; periods are
// ensured to exist by GetOrCreatePeriodCommand / close, so listing only shows what exists) ----
public sealed record GetAccountingPeriodsQuery : IRequest<Result<IReadOnlyList<AccountingPeriodDto>>>;

public sealed class GetAccountingPeriodsQueryHandler
    : IRequestHandler<GetAccountingPeriodsQuery, Result<IReadOnlyList<AccountingPeriodDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetAccountingPeriodsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> Handle(
        GetAccountingPeriodsQuery request, CancellationToken ct)
    {
        var items = await _db.AccountingPeriods.AsNoTracking()
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new AccountingPeriodDto(p.Id, p.Year, p.Month, p.IsClosed, p.ClosedBy, p.ClosedAt))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AccountingPeriodDto>>(items);
    }
}

// ---- Close a period. Administrator-only (enforced at the controller). SoD-style guard:
// the period must have no unposted (Draft) journal entries left in it — forcing cleanup
// before lock, which also trivially satisfies "the closer is not the sole preparer of
// unposted entries in the period" since none remain. ----
public sealed record ClosePeriodCommand(int Year, int Month) : IRequest<Result>;

public sealed class ClosePeriodCommandValidator : AbstractValidator<ClosePeriodCommand>
{
    public ClosePeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class ClosePeriodCommandHandler : IRequestHandler<ClosePeriodCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public ClosePeriodCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(ClosePeriodCommand request, CancellationToken ct)
    {
        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p => p.Year == request.Year && p.Month == request.Month, ct);

        if (period is null)
        {
            period = new AccountingPeriod { Year = request.Year, Month = request.Month };
            _db.AccountingPeriods.Add(period);
        }

        var start = new DateOnly(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        // Force cleanup first: a period with unposted (Draft) entries still in it cannot be
        // locked — otherwise the closer (possibly the sole preparer of those drafts) could
        // permanently strand them in a period nobody can post into again.
        var hasUnposted = await _db.JournalEntries.AsNoTracking()
            .AnyAsync(e => e.EntryDate >= start && e.EntryDate <= end && e.Status == JournalEntryStatus.Draft, ct);
        if (hasUnposted)
            return Result.Failure(Error.Conflict(
                $"Period {request.Year}-{request.Month:D2} has unposted journal entries. Post or void them before closing."));

        try { period.Close(_currentUser.UserId?.ToString(), _clock.UtcNow); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
