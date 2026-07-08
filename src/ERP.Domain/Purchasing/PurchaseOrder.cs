using ERP.Domain.Common;

namespace ERP.Domain.Purchasing;

/// <summary>
/// A purchase order — aggregate root over its lines. Enforces its lifecycle and the rule
/// that goods received never exceed what was ordered. Receiving is recorded per line and
/// advances the status to PartiallyReceived or Received automatically.
/// </summary>
public class PurchaseOrder : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    /// <summary>Warehouse the goods are received into.</summary>
    public Guid WarehouseId { get; set; }

    public DateOnly OrderDate { get; set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;

    /// <summary>User who approved (confirmed) this order. Segregation of duties compares against it.</summary>
    public string? ConfirmedBy { get; private set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

    public decimal SubTotal => Lines.Sum(l => l.LineTotal);

    public void AddLine(Guid productId, decimal quantity, decimal unitCost)
    {
        EnsureDraft("add lines to");
        if (quantity <= 0) throw new DomainException("Line quantity must be positive.");
        if (unitCost < 0) throw new DomainException("Line unit cost cannot be negative.");

        Lines.Add(new PurchaseOrderLine
        {
            PurchaseOrderId = Id,
            ProductId = productId,
            Quantity = quantity,
            UnitCost = unitCost
        });
    }

    public void Confirm(string? approvedBy = null)
    {
        EnsureDraft("confirm");
        if (Lines.Count == 0) throw new DomainException("Cannot confirm an order with no lines.");
        Status = PurchaseOrderStatus.Confirmed;
        ConfirmedBy = approvedBy;
    }

    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived
            or PurchaseOrderStatus.Cancelled)
            throw new DomainException($"A {Status} order cannot be cancelled.");
        Status = PurchaseOrderStatus.Cancelled;
    }

    /// <summary>
    /// Records receipt of <paramref name="quantity"/> against a specific line. The caller
    /// (application layer) then adds the stock. Throws if the line is unknown, the order is
    /// not in a receivable state, or the receipt would exceed the outstanding quantity.
    /// </summary>
    public PurchaseOrderLine ReceiveLine(Guid lineId, decimal quantity)
    {
        if (Status is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyReceived))
            throw new DomainException($"Cannot receive against a {Status} order.");
        if (quantity <= 0)
            throw new DomainException("Received quantity must be positive.");

        var line = Lines.FirstOrDefault(l => l.Id == lineId)
                   ?? throw new DomainException("Line does not belong to this order.");
        if (quantity > line.OutstandingQuantity)
            throw new DomainException(
                $"Cannot receive {quantity}; only {line.OutstandingQuantity} outstanding on the line.");

        line.QuantityReceived += quantity;
        RecomputeReceiptStatus();
        return line;
    }

    private void RecomputeReceiptStatus()
        => Status = Lines.All(l => l.OutstandingQuantity == 0)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;

    private void EnsureDraft(string action)
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new DomainException($"Cannot {action} a {Status} order; only drafts are editable.");
    }
}
