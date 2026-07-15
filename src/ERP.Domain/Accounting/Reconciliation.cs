using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>
/// Records the outcome of matching a <see cref="BankStatementLine"/> to a
/// <see cref="JournalLine"/> posted to the Cash account. One row per match; unmatching a
/// statement line does not delete this row (audit trail), it simply has no live effect once
/// the statement line itself reports unmatched again.
/// </summary>
public class Reconciliation : AuditableEntity
{
    public Guid BankStatementLineId { get; set; }
    public Guid MatchedJournalLineId { get; set; }

    public DateTimeOffset MatchedAt { get; set; }
    public string? MatchedBy { get; set; }
}
