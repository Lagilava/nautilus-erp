using ERP.Domain.Common;

namespace ERP.Domain.Purchasing;

/// <summary>
/// A line on a purchase order. Tracks how much has been received so far, so partial
/// deliveries are supported and the outstanding quantity is always known.
/// </summary>
public class PurchaseOrderLine : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }

    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Cumulative quantity received against this line across goods receipts.</summary>
    public decimal QuantityReceived { get; set; }

    public decimal OutstandingQuantity => Quantity - QuantityReceived;
    public decimal LineTotal => Quantity * UnitCost;
}
