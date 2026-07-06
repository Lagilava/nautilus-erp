using ERP.Domain.Common;
using ERP.Domain.Purchasing;

namespace ERP.UnitTests.Domain;

public class PurchaseOrderTests
{
    private static (PurchaseOrder order, Guid lineId) ConfirmedOrder(decimal qty = 10)
    {
        var order = new PurchaseOrder { OrderDate = new DateOnly(2026, 7, 1) };
        order.AddLine(Guid.NewGuid(), qty, 4m);
        order.Confirm();
        return (order, order.Lines.First().Id);
    }

    [Fact]
    public void Partial_receipt_moves_to_partially_received_then_received()
    {
        var (order, lineId) = ConfirmedOrder(qty: 10);

        order.ReceiveLine(lineId, 4);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(6m, order.Lines.First().OutstandingQuantity);

        order.ReceiveLine(lineId, 6);
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(0m, order.Lines.First().OutstandingQuantity);
    }

    [Fact]
    public void Cannot_over_receive_a_line()
    {
        var (order, lineId) = ConfirmedOrder(qty: 5);
        Assert.Throws<DomainException>(() => order.ReceiveLine(lineId, 6));
    }

    [Fact]
    public void Cannot_receive_against_a_draft_order()
    {
        var order = new PurchaseOrder();
        order.AddLine(Guid.NewGuid(), 5, 1m);
        var lineId = order.Lines.First().Id;
        Assert.Throws<DomainException>(() => order.ReceiveLine(lineId, 1));
    }

    [Fact]
    public void Received_order_cannot_be_cancelled()
    {
        var (order, lineId) = ConfirmedOrder(qty: 3);
        order.ReceiveLine(lineId, 3);
        Assert.Throws<DomainException>(() => order.Cancel());
    }

    [Fact]
    public void Cannot_add_lines_after_confirm()
    {
        var (order, _) = ConfirmedOrder();
        Assert.Throws<DomainException>(() => order.AddLine(Guid.NewGuid(), 1, 1));
    }
}
