namespace ERP.Domain.Sales;

/// <summary>
/// Lifecycle of a sales order. Draft is editable; Confirmed is committed; Fulfilled means
/// stock has been issued; Cancelled is terminal. Transitions are enforced on the aggregate.
/// </summary>
public enum SalesOrderStatus
{
    Draft = 1,
    Confirmed = 2,
    Fulfilled = 3,
    Cancelled = 4
}
