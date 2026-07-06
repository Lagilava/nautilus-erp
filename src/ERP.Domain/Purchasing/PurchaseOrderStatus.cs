namespace ERP.Domain.Purchasing;

/// <summary>
/// Lifecycle of a purchase order. Draft is editable; Confirmed is placed with the supplier;
/// PartiallyReceived/Received track goods-in progress; Cancelled is terminal (before receipt).
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 1,
    Confirmed = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
}
