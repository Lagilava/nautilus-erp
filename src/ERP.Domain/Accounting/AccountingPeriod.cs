using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>
/// A calendar-month accounting period that can be locked once its books are settled. Once
/// closed, no journal entry (manual or auto-posted) may be dated inside it — enforced by
/// callers checking <see cref="IsClosed"/> for the entry's <c>EntryDate</c>, mirroring the
/// effective-dated precedent set by <c>Tax</c>/<c>TaxRate</c>.
/// </summary>
public class AccountingPeriod : AuditableEntity
{
    public int Year { get; set; }
    public int Month { get; set; }

    public bool IsClosed { get; private set; }
    public string? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public DateOnly StartDate => new(Year, Month, 1);
    public DateOnly EndDate => StartDate.AddMonths(1).AddDays(-1);

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public void Close(string? closedBy, DateTimeOffset closedAt)
    {
        if (IsClosed)
            throw new DomainException($"Period {Year}-{Month:D2} is already closed.");

        IsClosed = true;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
    }

    public void Reopen()
    {
        if (!IsClosed)
            throw new DomainException($"Period {Year}-{Month:D2} is not closed.");

        IsClosed = false;
        ClosedBy = null;
        ClosedAt = null;
    }
}
