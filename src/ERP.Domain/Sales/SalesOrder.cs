using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>
/// A customer sales order — the aggregate root over its lines. Enforces its lifecycle:
/// a Draft can be edited, confirmed, or cancelled; a Confirmed order can be fulfilled
/// (stock issued by the application layer) or cancelled; Fulfilled and Cancelled are
/// terminal. Line edits are only allowed while Draft.
/// </summary>
public class SalesOrder : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    /// <summary>Warehouse the goods are fulfilled from.</summary>
    public Guid WarehouseId { get; set; }

    public DateOnly OrderDate { get; set; }
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;

    public string? Notes { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();

    /// <summary>Net order value before tax.</summary>
    public decimal SubTotal => Lines.Sum(l => l.LineTotal);

    public void AddLine(Guid productId, decimal quantity, decimal unitPrice)
    {
        EnsureDraft("add lines to");
        if (quantity <= 0) throw new DomainException("Line quantity must be positive.");
        if (unitPrice < 0) throw new DomainException("Line unit price cannot be negative.");

        Lines.Add(new SalesOrderLine
        {
            SalesOrderId = Id,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        });
    }

    public void Confirm()
    {
        EnsureDraft("confirm");
        if (Lines.Count == 0) throw new DomainException("Cannot confirm an order with no lines.");
        Status = SalesOrderStatus.Confirmed;
    }

    /// <summary>Marks the order fulfilled. Stock issuing is performed by the application layer.</summary>
    public void MarkFulfilled()
    {
        if (Status != SalesOrderStatus.Confirmed)
            throw new DomainException($"Only a confirmed order can be fulfilled (current: {Status}).");
        Status = SalesOrderStatus.Fulfilled;
    }

    public void Cancel()
    {
        if (Status is SalesOrderStatus.Fulfilled or SalesOrderStatus.Cancelled)
            throw new DomainException($"A {Status} order cannot be cancelled.");
        Status = SalesOrderStatus.Cancelled;
    }

    private void EnsureDraft(string action)
    {
        if (Status != SalesOrderStatus.Draft)
            throw new DomainException($"Cannot {action} a {Status} order; only drafts are editable.");
    }
}
