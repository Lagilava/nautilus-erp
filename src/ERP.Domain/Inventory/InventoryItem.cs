using ERP.Domain.Common;

namespace ERP.Domain.Inventory;

/// <summary>
/// Stock of one product in one warehouse — the aggregate root for inventory at a location.
/// Owns its FIFO cost layers and enforces the invariant that on-hand quantity never goes
/// negative. All quantity changes go through <see cref="Receive"/> / <see cref="Issue"/>,
/// which keep <see cref="QuantityOnHand"/> and the layers consistent. Costing is FIFO:
/// issues consume the earliest-received layers first.
/// </summary>
public class InventoryItem : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }

    public decimal QuantityOnHand { get; private set; }

    /// <summary>Threshold at or below which the item is flagged for reordering.</summary>
    public decimal ReorderLevel { get; set; }

    /// <summary>Monotonic counter issuing FIFO sequence numbers to new layers.</summary>
    public long LastSequenceNumber { get; private set; }

    public ICollection<StockLayer> Layers { get; set; } = new List<StockLayer>();

    public bool IsBelowReorder => QuantityOnHand <= ReorderLevel;

    /// <summary>Current inventory value: the sum of remaining quantity × unit cost across layers.</summary>
    public decimal ValueOnHand => Layers.Sum(l => l.RemainingQuantity * l.UnitCost);

    /// <summary>
    /// Adds stock at a known unit cost, creating a new FIFO layer. Returns the total cost added.
    /// </summary>
    public decimal Receive(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");

        Layers.Add(new StockLayer
        {
            InventoryItemId = Id,
            SequenceNumber = ++LastSequenceNumber,
            UnitCost = unitCost,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity
        });

        QuantityOnHand += quantity;
        return quantity * unitCost;
    }

    /// <summary>
    /// Removes stock, consuming FIFO layers oldest-first. Returns the cost of goods removed.
    /// Throws <see cref="InsufficientStockException"/> if there is not enough on hand.
    /// </summary>
    public decimal Issue(decimal quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (quantity > QuantityOnHand) throw new InsufficientStockException(quantity, QuantityOnHand);

        decimal remainingToIssue = quantity;
        decimal cost = 0m;

        foreach (var layer in Layers.Where(l => l.RemainingQuantity > 0).OrderBy(l => l.SequenceNumber))
        {
            if (remainingToIssue <= 0) break;

            var taken = Math.Min(layer.RemainingQuantity, remainingToIssue);
            layer.RemainingQuantity -= taken;
            cost += taken * layer.UnitCost;
            remainingToIssue -= taken;
        }

        QuantityOnHand -= quantity;
        return cost;
    }
}
