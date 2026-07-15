using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>
/// A double-entry journal entry — the aggregate root over its <see cref="JournalLine"/>s.
/// Mirrors <c>Invoice</c>'s Draft→committed lifecycle: <see cref="Post"/> commits it as a
/// permanent ledger record once debits equal credits; <see cref="Void"/> never mutates a
/// posted entry, it writes a reversing entry instead (accounting-immutability, same spirit
/// as invoices never being deleted). Branch-scoped so one branch's postings never leak into
/// another branch's trial balance.
/// </summary>
public class JournalEntry : AuditableEntity
{
    public Guid? BranchId { get; set; }

    public DateOnly EntryDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }

    public JournalEntryStatus Status { get; private set; } = JournalEntryStatus.Draft;
    public JournalEntrySource Source { get; set; } = JournalEntrySource.Manual;

    /// <summary>Originating Invoice/SupplierInvoice/Payment id, for traceability. Null for manual entries.</summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>User who created a manual entry. Null for system auto-posted entries.</summary>
    public string? PreparedBy { get; set; }

    /// <summary>User who posted the entry. Null for system auto-posted entries (no human posts them).</summary>
    public string? PostedBy { get; private set; }

    /// <summary>The entry this one reverses, set on the reversing entry written by <see cref="Void"/>.</summary>
    public Guid? ReversalOfJournalEntryId { get; set; }

    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

    public decimal TotalDebits => Lines.Sum(l => l.Debit);
    public decimal TotalCredits => Lines.Sum(l => l.Credit);

    public void AddLine(
        Guid accountId, decimal debit, decimal credit, string? memo = null,
        Guid? currencyId = null, decimal? exchangeRate = null)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new DomainException($"Cannot edit a {Status} journal entry; only drafts are editable.");
        if (debit < 0 || credit < 0)
            throw new DomainException("Debit/credit amounts cannot be negative.");
        if (debit > 0 && credit > 0)
            throw new DomainException("A journal line cannot be both a debit and a credit.");
        if (debit == 0 && credit == 0)
            throw new DomainException("A journal line must have a non-zero debit or credit.");
        if (exchangeRate is <= 0)
            throw new DomainException("Exchange rate must be positive.");

        Lines.Add(new JournalLine
        {
            JournalEntryId = Id,
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
            Memo = memo,
            CurrencyId = currencyId,
            ExchangeRate = currencyId is null ? null : exchangeRate ?? 1m
        });
    }

    /// <summary>
    /// Commits the entry: enforces the fundamental double-entry invariant (total debits ==
    /// total credits across all lines) in the domain, not just the database.
    /// </summary>
    public void Post(string? postedBy = null)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new DomainException($"Only a draft journal entry can be posted (current: {Status}).");
        if (Lines.Count == 0)
            throw new DomainException("Cannot post a journal entry with no lines.");
        if (TotalDebits != TotalCredits)
            throw new DomainException(
                $"Journal entry is not balanced: debits {TotalDebits} != credits {TotalCredits}.");

        Status = JournalEntryStatus.Posted;
        PostedBy = postedBy;
    }

    /// <summary>
    /// Voids a posted entry. Never mutates this entry's lines — the caller must build and post
    /// a separate reversing <see cref="JournalEntry"/> with flipped debit/credit lines that
    /// references this one via <see cref="ReversalOfJournalEntryId"/>.
    /// </summary>
    public void Void()
    {
        if (Status != JournalEntryStatus.Posted)
            throw new DomainException($"Only a posted journal entry can be voided (current: {Status}).");
        Status = JournalEntryStatus.Voided;
    }

    /// <summary>Builds (but does not post) the reversing entry for a posted journal entry.</summary>
    public JournalEntry BuildReversal(DateOnly reversalDate, string? reference = null)
    {
        if (Status != JournalEntryStatus.Voided && Status != JournalEntryStatus.Posted)
            throw new DomainException("Only a posted (or being-voided) journal entry can be reversed.");

        var reversal = new JournalEntry
        {
            BranchId = BranchId,
            EntryDate = reversalDate,
            Reference = reference ?? $"REVERSAL-{Reference}",
            Description = $"Reversal of {Reference}",
            Source = Source,
            SourceDocumentId = SourceDocumentId,
            ReversalOfJournalEntryId = Id
        };

        foreach (var line in Lines)
            reversal.AddLine(line.AccountId, line.Credit, line.Debit, line.Memo, line.CurrencyId, line.ExchangeRate);

        return reversal;
    }
}
