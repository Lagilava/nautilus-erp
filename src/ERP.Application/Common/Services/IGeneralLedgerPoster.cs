using ERP.Domain.Purchasing;
using ERP.Domain.Sales;

namespace ERP.Application.Common.Services;

/// <summary>Codes of the system accounts seeded for auto-posting. See <c>DemoDataSeeder</c>/migration seed.</summary>
public static class GeneralLedgerAccountCodes
{
    public const string Cash = "1000";
    public const string AccountsReceivable = "1100";
    public const string Inventory = "1200";
    public const string AccountsPayable = "2100";
    public const string SalesTaxPayable = "2200";
    public const string SalesRevenue = "4000";
    public const string CostOfGoodsSold = "5000";
}

/// <summary>
/// Posts journal entries for existing sales/purchasing business events. Writes
/// <c>JournalEntry</c>/<c>JournalLine</c> rows via <see cref="Interfaces.IApplicationDbContext"/>
/// in the same unit of work as the caller, keeping posting transactional with the business
/// event. These entries are system-posted (no human "preparer"), so they are created already
/// <c>Posted</c> and skip the segregation-of-duties check that manual entries go through.
/// </summary>
public interface IGeneralLedgerPoster
{
    /// <summary>Dr Accounts Receivable, Cr Sales Revenue (+ Cr Sales Tax Payable if taxed).</summary>
    Task PostSalesInvoiceIssuedAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Dr Cash, Cr Accounts Receivable.</summary>
    Task PostSalesPaymentAsync(Payment payment, CancellationToken ct = default);

    /// <summary>Dr Cost of Goods Sold/Inventory per line, Cr Accounts Payable.</summary>
    Task PostSupplierInvoiceApprovedAsync(SupplierInvoice invoice, CancellationToken ct = default);

    /// <summary>Dr Accounts Payable, Cr Cash.</summary>
    Task PostSupplierPaymentAsync(SupplierPayment payment, CancellationToken ct = default);
}
