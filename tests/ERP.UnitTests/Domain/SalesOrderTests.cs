using ERP.Domain.Common;
using ERP.Domain.Sales;

namespace ERP.UnitTests.Domain;

public class SalesOrderTests
{
    private static SalesOrder OrderWithLine()
    {
        var order = new SalesOrder { OrderDate = new DateOnly(2026, 7, 1) };
        order.AddLine(Guid.NewGuid(), 5, 10m);
        return order;
    }

    [Fact]
    public void New_order_is_draft_and_totals_its_lines()
    {
        var order = OrderWithLine();
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
        Assert.Equal(50m, order.SubTotal);
    }

    [Fact]
    public void Confirm_requires_lines_and_moves_to_confirmed()
    {
        Assert.Throws<DomainException>(() => new SalesOrder().Confirm());

        var order = OrderWithLine();
        order.Confirm();
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Cannot_add_lines_after_confirm()
    {
        var order = OrderWithLine();
        order.Confirm();
        Assert.Throws<DomainException>(() => order.AddLine(Guid.NewGuid(), 1, 1));
    }

    [Fact]
    public void Only_confirmed_orders_can_be_fulfilled()
    {
        var order = OrderWithLine();
        Assert.Throws<DomainException>(() => order.MarkFulfilled()); // still draft

        order.Confirm();
        order.MarkFulfilled();
        Assert.Equal(SalesOrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void Fulfilled_order_cannot_be_cancelled()
    {
        var order = OrderWithLine();
        order.Confirm();
        order.MarkFulfilled();
        Assert.Throws<DomainException>(() => order.Cancel());
    }
}
