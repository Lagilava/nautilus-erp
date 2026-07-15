using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Application.Common.Services;
using ERP.Domain.Accounting;
using ERP.Domain.Common;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.JournalEntries;

// ---- Post a draft manual journal entry ----
public sealed record PostJournalEntryCommand(Guid Id) : IRequest<Result>;

public sealed class PostJournalEntryCommandHandler : IRequestHandler<PostJournalEntryCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ISegregationOfDuties _sod;
    private readonly ICurrentUserService _currentUser;

    public PostJournalEntryCommandHandler(
        IApplicationDbContext db, ISegregationOfDuties sod, ICurrentUserService currentUser)
    {
        _db = db;
        _sod = sod;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(PostJournalEntryCommand request, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (entry is null) return Result.Failure(Error.NotFound("Journal entry not found."));

        // The preparer of a manual entry may not also post it — otherwise one person books and
        // commits a ledger entry alone, with no second set of eyes on the postings.
        var sod = _sod.Ensure(SoDRule.JournalEntryPosting,
            "You cannot post a journal entry you prepared. It must be posted by someone else.",
            entry.PreparedBy);
        if (sod.IsFailure) return sod;

        try
        {
            await AccountingPeriodGuard.EnsureOpenAsync(_db, entry.EntryDate, ct);
            entry.Post(_currentUser.UserId?.ToString());
        }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Void a posted journal entry (writes a reversing entry; never mutates the original) ----
public sealed record VoidJournalEntryCommand(Guid Id) : IRequest<Result<Guid>>;

public sealed class VoidJournalEntryCommandHandler : IRequestHandler<VoidJournalEntryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public VoidJournalEntryCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(VoidJournalEntryCommand request, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (entry is null) return Result.Failure<Guid>(Error.NotFound("Journal entry not found."));

        JournalEntry reversal;
        try
        {
            reversal = entry.BuildReversal(DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
            entry.Void();
            reversal.Post();
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Conflict(ex.Message));
        }

        _db.JournalEntries.Add(reversal);
        await _db.SaveChangesAsync(ct);
        return Result.Success(reversal.Id);
    }
}
