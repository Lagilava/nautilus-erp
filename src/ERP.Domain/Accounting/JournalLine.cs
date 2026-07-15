using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>
/// One side of a journal entry's double-entry postings. Exactly one of <see cref="Debit"/>/
/// <see cref="Credit"/> is normally non-zero per line, but both default to zero and the
/// entry-level invariant (total debits == total credits) is enforced by
/// <see cref="JournalEntry.Post"/>, not here.
/// </summary>
public class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public string? Memo { get; set; }

    // Phase 2: multi-currency. Null CurrencyId/ExchangeRate == 1 means the base currency.
    public Guid? CurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
}
