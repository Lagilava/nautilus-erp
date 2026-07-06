using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>A line on a sales order: a product, a quantity, and the agreed unit price.</summary>
public class SalesOrderLine : BaseEntity
{
    public Guid SalesOrderId { get; set; }

    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Net line amount before tax (quantity × unit price).</summary>
    public decimal LineTotal => Quantity * UnitPrice;
}
