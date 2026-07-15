using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>How a bank statement line entered the system.</summary>
public enum BankStatementLineSource
{
    Imported = 1,
    Manual = 2
}

/// <summary>
/// One line of a bank statement (imported or keyed in manually), to be reconciled against a
/// <see cref="JournalLine"/> posted to the Cash account. Never mutated once matched — the
/// match itself is recorded on the paired <see cref="Reconciliation"/> row.
/// </summary>
public class BankStatementLine : AuditableEntity
{
    public DateOnly StatementDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public BankStatementLineSource Source { get; set; } = BankStatementLineSource.Manual;

    /// <summary>Cash-account journal line this statement line has been matched to, if any.</summary>
    public Guid? MatchedJournalLineId { get; private set; }

    public bool IsMatched => MatchedJournalLineId.HasValue;

    public void Match(Guid journalLineId)
    {
        if (IsMatched)
            throw new DomainException("This statement line is already matched to a journal line.");
        MatchedJournalLineId = journalLineId;
    }

    public void Unmatch()
    {
        if (!IsMatched)
            throw new DomainException("This statement line is not matched.");
        MatchedJournalLineId = null;
    }
}
