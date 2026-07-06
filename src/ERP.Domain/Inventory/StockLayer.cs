using ERP.Domain.Common;

namespace ERP.Domain.Inventory;

/// <summary>
/// A FIFO cost layer: a batch of stock received at a specific unit cost. Issues consume
/// layers oldest-first (by <see cref="SequenceNumber"/>), so cost of goods sold reflects
/// the actual cost of the earliest-received units. A layer is exhausted when
/// <see cref="RemainingQuantity"/> reaches zero.
/// </summary>
public class StockLayer : BaseEntity
{
    public Guid InventoryItemId { get; set; }

    /// <summary>Monotonic per-item ordering key establishing FIFO order (received order).</summary>
    public long SequenceNumber { get; set; }

    public decimal UnitCost { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }

    public bool IsExhausted => RemainingQuantity <= 0m;
}
