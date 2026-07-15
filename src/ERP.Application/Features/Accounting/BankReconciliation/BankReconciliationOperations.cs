using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Domain.Accounting;
using ERP.Domain.Common;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Accounting.BankReconciliation;

public sealed record BankStatementLineDto(
    Guid Id, DateOnly StatementDate, decimal Amount, string? Description,
    BankStatementLineSource Source, bool IsMatched, Guid? MatchedJournalLineId);

public sealed record UnreconciledJournalLineDto(
    Guid JournalLineId, Guid JournalEntryId, DateOnly EntryDate, string Reference,
    decimal Debit, decimal Credit, string? Memo);

// ---- Create a statement line (imported or manually keyed) ----
public sealed record CreateBankStatementLineCommand(
    DateOnly StatementDate, decimal Amount, string? Description, BankStatementLineSource Source)
    : IRequest<Result<Guid>>;

public sealed class CreateBankStatementLineCommandValidator : AbstractValidator<CreateBankStatementLineCommand>
{
    public CreateBankStatementLineCommandValidator()
    {
        RuleFor(x => x.StatementDate).NotEmpty();
        RuleFor(x => x.Amount).NotEqual(0m);
        RuleFor(x => x.Source).IsInEnum();
    }
}

public sealed class CreateBankStatementLineCommandHandler
    : IRequestHandler<CreateBankStatementLineCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateBankStatementLineCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateBankStatementLineCommand request, CancellationToken ct)
    {
        var line = new BankStatementLine
        {
            StatementDate = request.StatementDate,
            Amount = request.Amount,
            Description = request.Description,
            Source = request.Source
        };
        _db.BankStatementLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success(line.Id);
    }
}

// ---- List unreconciled statement lines ----
public sealed record GetUnreconciledStatementLinesQuery : IRequest<Result<IReadOnlyList<BankStatementLineDto>>>;

public sealed class GetUnreconciledStatementLinesQueryHandler
    : IRequestHandler<GetUnreconciledStatementLinesQuery, Result<IReadOnlyList<BankStatementLineDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetUnreconciledStatementLinesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<BankStatementLineDto>>> Handle(
        GetUnreconciledStatementLinesQuery request, CancellationToken ct)
    {
        var items = await _db.BankStatementLines.AsNoTracking()
            .Where(l => l.MatchedJournalLineId == null)
            .OrderBy(l => l.StatementDate)
            .Select(l => new BankStatementLineDto(
                l.Id, l.StatementDate, l.Amount, l.Description, l.Source, l.IsMatched, l.MatchedJournalLineId))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<BankStatementLineDto>>(items);
    }
}

// ---- List unreconciled Cash-account journal lines (candidates to match against) ----
public sealed record GetUnreconciledCashJournalLinesQuery : IRequest<Result<IReadOnlyList<UnreconciledJournalLineDto>>>;

public sealed class GetUnreconciledCashJournalLinesQueryHandler
    : IRequestHandler<GetUnreconciledCashJournalLinesQuery, Result<IReadOnlyList<UnreconciledJournalLineDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetUnreconciledCashJournalLinesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<UnreconciledJournalLineDto>>> Handle(
        GetUnreconciledCashJournalLinesQuery request, CancellationToken ct)
    {
        var cashAccountId = await _db.Accounts.AsNoTracking()
            .Where(a => a.Code == GeneralLedgerAccountCodes.Cash)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(ct);
        if (cashAccountId == Guid.Empty)
            return Result.Success<IReadOnlyList<UnreconciledJournalLineDto>>([]);

        var reconciledLineIds = await _db.Reconciliations.AsNoTracking()
            .Select(r => r.MatchedJournalLineId)
            .ToListAsync(ct);

        var items = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines, (e, l) => new { Entry = e, Line = l })
            .Where(x => x.Line.AccountId == cashAccountId && !reconciledLineIds.Contains(x.Line.Id))
            .OrderBy(x => x.Entry.EntryDate)
            .Select(x => new UnreconciledJournalLineDto(
                x.Line.Id, x.Entry.Id, x.Entry.EntryDate, x.Entry.Reference, x.Line.Debit, x.Line.Credit, x.Line.Memo))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<UnreconciledJournalLineDto>>(items);
    }
}

// ---- Match a statement line to a Cash-account journal line ----
public sealed record MatchStatementLineCommand(Guid BankStatementLineId, Guid JournalLineId) : IRequest<Result>;

public sealed class MatchStatementLineCommandHandler : IRequestHandler<MatchStatementLineCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public MatchStatementLineCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(MatchStatementLineCommand request, CancellationToken ct)
    {
        var statementLine = await _db.BankStatementLines
            .FirstOrDefaultAsync(l => l.Id == request.BankStatementLineId, ct);
        if (statementLine is null) return Result.Failure(Error.NotFound("Bank statement line not found."));

        var journalLine = await _db.JournalLines.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.JournalLineId, ct);
        if (journalLine is null) return Result.Failure(Error.NotFound("Journal line not found."));

        var alreadyMatched = await _db.Reconciliations.AsNoTracking()
            .AnyAsync(r => r.MatchedJournalLineId == request.JournalLineId, ct);
        if (alreadyMatched)
            return Result.Failure(Error.Conflict("That journal line is already matched to a statement line."));

        // A statement deposit (positive amount) must match a Cash debit; a withdrawal
        // (negative amount) must match a Cash credit, and the magnitudes must agree —
        // otherwise the "match" would silently misstate the reconciled balance.
        var journalSignedAmount = journalLine.Debit - journalLine.Credit;
        if (journalSignedAmount != statementLine.Amount)
            return Result.Failure(Error.Validation(
                $"Amount mismatch: statement line is {statementLine.Amount:0.00}, journal line is {journalSignedAmount:0.00}."));

        try { statementLine.Match(journalLine.Id); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        _db.Reconciliations.Add(new Domain.Accounting.Reconciliation
        {
            BankStatementLineId = statementLine.Id,
            MatchedJournalLineId = journalLine.Id,
            MatchedAt = _clock.UtcNow,
            MatchedBy = _currentUser.UserId?.ToString()
        });

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Unmatch (undo a mistaken match) ----
public sealed record UnmatchStatementLineCommand(Guid BankStatementLineId) : IRequest<Result>;

public sealed class UnmatchStatementLineCommandHandler : IRequestHandler<UnmatchStatementLineCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public UnmatchStatementLineCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(UnmatchStatementLineCommand request, CancellationToken ct)
    {
        var statementLine = await _db.BankStatementLines
            .FirstOrDefaultAsync(l => l.Id == request.BankStatementLineId, ct);
        if (statementLine is null) return Result.Failure(Error.NotFound("Bank statement line not found."));

        try { statementLine.Unmatch(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
