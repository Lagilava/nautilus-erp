using ERP.Domain.Common;

namespace ERP.Domain.Purchasing;

/// <summary>A goods-receipt line: how much of a product was received (against a PO line).</summary>
public class GoodsReceiptLine : BaseEntity
{
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// A record of goods physically received against a purchase order. Immutable once posted;
/// posting increments warehouse stock (a FIFO layer per line) and the PO's received quantities.
/// </summary>
public class GoodsReceipt : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid PurchaseOrderId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}
