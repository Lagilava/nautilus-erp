using ERP.Domain.Common;
using ERP.Domain.Sales;

namespace ERP.Domain.Purchasing;

/// <summary>
/// A payment made to a supplier against a supplier invoice. Immutable once recorded.
/// Reuses the shared <see cref="PaymentMethod"/> (cash, card, bank transfer, wallet, cheque).
/// </summary>
public class SupplierPayment : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid SupplierInvoiceId { get; set; }
    public Guid SupplierId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
}
