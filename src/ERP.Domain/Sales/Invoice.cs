using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>
/// A customer invoice — the aggregate root over its lines and the money owed. Enforces its
/// lifecycle (Draft → Issued → PartiallyPaid → Paid, or Void before payment) and the
/// arithmetic of what has been paid. Totals derive from the lines, whose tax rates are
/// snapshotted at issue so the document never changes retroactively.
/// </summary>
public class Invoice : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    /// <summary>Originating sales order, if the invoice was raised from one.</summary>
    public Guid? SalesOrderId { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    public decimal AmountPaid { get; private set; }

    // Fiscalization (FRCS/VMS) — set by the fiscalization service on issue.
    public FiscalStatus FiscalStatus { get; private set; } = FiscalStatus.NotSubmitted;
    public string? FiscalReference { get; private set; }

    public string? Notes { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    public decimal SubTotal => Lines.Sum(l => l.LineSubTotal);
    public decimal TaxTotal => Lines.Sum(l => l.LineTax);
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public decimal Balance => Total - AmountPaid;

    public void AddLine(Guid productId, string description, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException($"Cannot edit a {Status} invoice; only drafts are editable.");
        if (quantity <= 0) throw new DomainException("Line quantity must be positive.");
        if (unitPrice < 0) throw new DomainException("Line unit price cannot be negative.");
        if (taxRate < 0) throw new DomainException("Tax rate cannot be negative.");

        Lines.Add(new InvoiceLine
        {
            InvoiceId = Id,
            ProductId = productId,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate
        });
    }

    /// <summary>Commits the invoice as a tax document. No further line edits are allowed.</summary>
    public void Issue()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException($"Only a draft invoice can be issued (current: {Status}).");
        if (Lines.Count == 0)
            throw new DomainException("Cannot issue an invoice with no lines.");
        Status = InvoiceStatus.Issued;
    }

    /// <summary>Records the outcome of an FRCS/VMS submission attempt.</summary>
    public void SetFiscalResult(FiscalStatus status, string? reference)
    {
        FiscalStatus = status;
        FiscalReference = reference;
    }

    /// <summary>Applies a payment, advancing the status to PartiallyPaid or Paid.</summary>
    public void ApplyPayment(decimal amount)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
            throw new DomainException($"Cannot pay a {Status} invoice.");
        if (amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        if (amount > Balance)
            throw new DomainException($"Payment {amount} exceeds outstanding balance {Balance}.");

        AmountPaid += amount;
        Status = Balance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }

    public void Void()
    {
        if (Status == InvoiceStatus.Paid || AmountPaid > 0)
            throw new DomainException("A paid or part-paid invoice cannot be voided.");
        if (Status == InvoiceStatus.Void)
            throw new DomainException("Invoice is already void.");
        Status = InvoiceStatus.Void;
    }
}
