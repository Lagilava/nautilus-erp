using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Domain.Accounting;
using ERP.Domain.Common;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Accounting.JournalEntries;

/// <summary>
/// One input line for a manual journal entry. <paramref name="CurrencyId"/>/<paramref name="ExchangeRate"/>
/// are optional — omitted (or null) means the line is in the base currency at rate 1, which is
/// how every pre-multi-currency entry continues to behave.
/// </summary>
public sealed record ManualJournalLineInput(
    Guid AccountId, decimal Debit, decimal Credit, string? Memo,
    Guid? CurrencyId = null, decimal? ExchangeRate = null);

/// <summary>
/// Creates a draft manual journal entry. It is not posted here — posting is a separate,
/// SoD-guarded step (<see cref="PostJournalEntryCommand"/>) so the preparer and poster can
/// be different people.
/// </summary>
public sealed record CreateManualJournalEntryCommand(
    DateOnly EntryDate, string Reference, string? Description, IReadOnlyList<ManualJournalLineInput> Lines)
    : IRequest<Result<Guid>>;

public sealed class CreateManualJournalEntryCommandValidator : AbstractValidator<CreateManualJournalEntryCommand>
{
    public CreateManualJournalEntryCommandValidator()
    {
        RuleFor(x => x.EntryDate).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
    }
}

public sealed class CreateManualJournalEntryCommandHandler
    : IRequestHandler<CreateManualJournalEntryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateManualJournalEntryCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateManualJournalEntryCommand request, CancellationToken ct)
    {
        try
        {
            await AccountingPeriodGuard.EnsureOpenAsync(_db, request.EntryDate, ct);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Conflict(ex.Message));
        }

        var entry = new JournalEntry
        {
            BranchId = _currentUser.BranchId,
            EntryDate = request.EntryDate,
            Reference = request.Reference,
            Description = request.Description,
            Source = JournalEntrySource.Manual,
            PreparedBy = _currentUser.UserId?.ToString()
        };

        try
        {
            foreach (var line in request.Lines)
                entry.AddLine(line.AccountId, line.Debit, line.Credit, line.Memo, line.CurrencyId, line.ExchangeRate);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation(ex.Message));
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return Result.Success(entry.Id);
    }
}
