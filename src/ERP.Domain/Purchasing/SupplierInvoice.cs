using ERP.Domain.Common;

namespace ERP.Domain.Purchasing;

/// <summary>
/// Lifecycle of a supplier invoice (accounts payable). Draft is editable; Approved is a
/// committed liability; payments move it to PartiallyPaid then Paid; Cancelled before payment.
/// </summary>
public enum SupplierInvoiceStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

/// <summary>A supplier-invoice line. Tax rate is snapshotted for input-VAT records.</summary>
public class SupplierInvoiceLine : BaseEntity
{
    public Guid SupplierInvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }

    public decimal LineSubTotal => Quantity * UnitCost;
    public decimal LineTax => Math.Round(LineSubTotal * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal LineTotal => LineSubTotal + LineTax;
}

/// <summary>
/// A bill received from a supplier — aggregate root over its lines and what has been paid.
/// Mirrors the customer <c>Invoice</c> on the payable side, with the same lifecycle and
/// payment arithmetic. No fiscalization applies (that is a sales-side/output-VAT concern).
/// </summary>
public class SupplierInvoice : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Supplier's own invoice reference, for reconciliation.</summary>
    public string? SupplierReference { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public SupplierInvoiceStatus Status { get; private set; } = SupplierInvoiceStatus.Draft;
    public decimal AmountPaid { get; private set; }

    public ICollection<SupplierInvoiceLine> Lines { get; set; } = new List<SupplierInvoiceLine>();

    public decimal SubTotal => Lines.Sum(l => l.LineSubTotal);
    public decimal TaxTotal => Lines.Sum(l => l.LineTax);
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public decimal Balance => Total - AmountPaid;

    public void AddLine(Guid productId, string description, decimal quantity, decimal unitCost, decimal taxRate)
    {
        if (Status != SupplierInvoiceStatus.Draft)
            throw new DomainException($"Cannot edit a {Status} supplier invoice; only drafts are editable.");
        if (quantity <= 0) throw new DomainException("Line quantity must be positive.");
        if (unitCost < 0) throw new DomainException("Line unit cost cannot be negative.");
        if (taxRate < 0) throw new DomainException("Tax rate cannot be negative.");

        Lines.Add(new SupplierInvoiceLine
        {
            SupplierInvoiceId = Id,
            ProductId = productId,
            Description = description,
            Quantity = quantity,
            UnitCost = unitCost,
            TaxRate = taxRate
        });
    }

    public void Approve()
    {
        if (Status != SupplierInvoiceStatus.Draft)
            throw new DomainException($"Only a draft supplier invoice can be approved (current: {Status}).");
        if (Lines.Count == 0)
            throw new DomainException("Cannot approve a supplier invoice with no lines.");
        Status = SupplierInvoiceStatus.Approved;
    }

    public void ApplyPayment(decimal amount)
    {
        if (Status is not (SupplierInvoiceStatus.Approved or SupplierInvoiceStatus.PartiallyPaid))
            throw new DomainException($"Cannot pay a {Status} supplier invoice.");
        if (amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        if (amount > Balance)
            throw new DomainException($"Payment {amount} exceeds outstanding balance {Balance}.");

        AmountPaid += amount;
        Status = Balance == 0 ? SupplierInvoiceStatus.Paid : SupplierInvoiceStatus.PartiallyPaid;
    }

    public void Cancel()
    {
        if (Status == SupplierInvoiceStatus.Paid || AmountPaid > 0)
            throw new DomainException("A paid or part-paid supplier invoice cannot be cancelled.");
        if (Status == SupplierInvoiceStatus.Cancelled)
            throw new DomainException("Supplier invoice is already cancelled.");
        Status = SupplierInvoiceStatus.Cancelled;
    }
}
