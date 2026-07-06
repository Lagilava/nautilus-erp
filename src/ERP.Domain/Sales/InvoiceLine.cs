using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>
/// A line on an invoice. The tax rate is <b>snapshotted</b> at the rate in force on the
/// invoice's issue date, so the document is immutable against later rate changes — a
/// requirement for a compliant tax document.
/// </summary>
public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Guid ProductId { get; set; }
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Tax percentage applied to this line (e.g. 15.00), snapshotted at issue.</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Net amount before tax.</summary>
    public decimal LineSubTotal => Quantity * UnitPrice;

    /// <summary>Tax amount for the line.</summary>
    public decimal LineTax => Math.Round(LineSubTotal * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Gross line amount (net + tax).</summary>
    public decimal LineTotal => LineSubTotal + LineTax;
}
