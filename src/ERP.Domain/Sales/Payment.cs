using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>
/// A payment received against an invoice. Immutable once recorded; reversing a payment is
/// a separate (future) operation rather than an edit, preserving the financial audit trail.
/// </summary>
public class Payment : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }

    /// <summary>External reference (bank txn id, wallet reference, cheque number).</summary>
    public string? Reference { get; set; }
}
