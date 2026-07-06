using ERP.Domain.Inventory;

namespace ERP.UnitTests.Domain;

/// <summary>
/// The FIFO costing engine is the highest-risk logic in the system, so it is tested
/// directly on the aggregate: layer creation, oldest-first consumption, cost of goods
/// sold, cross-layer issues, valuation, and the no-negative-stock invariant.
/// </summary>
public class InventoryItemFifoTests
{
    private static InventoryItem NewItem() => new() { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid() };

    [Fact]
    public void Receive_creates_a_layer_and_increases_quantity_and_value()
    {
        var item = NewItem();
        var cost = item.Receive(10, 2.00m);

        Assert.Equal(20.00m, cost);
        Assert.Equal(10, item.QuantityOnHand);
        Assert.Equal(20.00m, item.ValueOnHand);
        Assert.Single(item.Layers);
    }

    [Fact]
    public void Issue_consumes_oldest_layer_first_and_returns_its_cost()
    {
        var item = NewItem();
        item.Receive(10, 2.00m);   // layer 1 @ 2.00
        item.Receive(10, 3.00m);   // layer 2 @ 3.00

        var cogs = item.Issue(5);  // wholly within layer 1

        Assert.Equal(10.00m, cogs);              // 5 × 2.00
        Assert.Equal(15, item.QuantityOnHand);
        Assert.Equal(40.00m, item.ValueOnHand);  // 5×2.00 + 10×3.00
    }

    [Fact]
    public void Issue_spanning_multiple_layers_blends_their_costs_in_fifo_order()
    {
        var item = NewItem();
        item.Receive(10, 2.00m);   // layer 1
        item.Receive(10, 3.00m);   // layer 2

        var cogs = item.Issue(15); // all of layer 1 (10×2) + 5 of layer 2 (5×3)

        Assert.Equal(35.00m, cogs);
        Assert.Equal(5, item.QuantityOnHand);
        Assert.Equal(15.00m, item.ValueOnHand);  // remaining 5 × 3.00
        Assert.True(item.Layers.First(l => l.UnitCost == 2.00m).IsExhausted);
    }

    [Fact]
    public void Issue_more_than_on_hand_throws_and_does_not_mutate_state()
    {
        var item = NewItem();
        item.Receive(5, 2.00m);

        Assert.Throws<InsufficientStockException>(() => item.Issue(6));
        Assert.Equal(5, item.QuantityOnHand);
        Assert.Equal(10.00m, item.ValueOnHand);
    }

    [Fact]
    public void IsBelowReorder_reflects_quantity_against_threshold()
    {
        var item = NewItem();
        item.ReorderLevel = 10;
        item.Receive(8, 1.00m);

        Assert.True(item.IsBelowReorder);
        item.Receive(5, 1.00m);      // now 13
        Assert.False(item.IsBelowReorder);
    }

    [Fact]
    public void Receive_rejects_non_positive_quantity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => NewItem().Receive(0, 1m));
}
