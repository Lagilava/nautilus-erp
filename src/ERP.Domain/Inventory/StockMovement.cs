using ERP.Domain.Common;

namespace ERP.Domain.Inventory;

/// <summary>
/// An immutable ledger entry recording one change to stock. Quantity is always positive;
/// the <see cref="MovementType"/> conveys direction. For inbound movements
/// <see cref="UnitCost"/> is the cost per unit; for outbound movements <see cref="TotalCost"/>
/// is the FIFO cost of goods removed.
/// </summary>
public class StockMovement : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }

    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Per-unit cost for inbound movements (Receipt/AdjustmentIn/TransferIn).</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Total cost moved: quantity × unit cost inbound, or FIFO COGS outbound.</summary>
    public decimal TotalCost { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Business reference (e.g. PO/invoice/transfer number) linking to the source document.</summary>
    public string? Reference { get; set; }

    public string? Notes { get; set; }
}
