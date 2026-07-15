using ERP.Application.Common.Interfaces;
using ERP.Domain.Accounting;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common.Services;

/// <inheritdoc cref="IGeneralLedgerPoster"/>
internal sealed class GeneralLedgerPoster : IGeneralLedgerPoster
{
    private readonly IApplicationDbContext _db;

    public GeneralLedgerPoster(IApplicationDbContext db) => _db = db;

    public async Task PostSalesInvoiceIssuedAsync(Invoice invoice, CancellationToken ct = default)
    {
        await AccountingPeriodGuard.EnsureOpenAsync(_db, invoice.IssueDate, ct);

        var accounts = await ResolveAsync(ct,
            GeneralLedgerAccountCodes.AccountsReceivable,
            GeneralLedgerAccountCodes.SalesRevenue,
            GeneralLedgerAccountCodes.SalesTaxPayable);

        var entry = NewEntry(invoice.IssueDate, invoice.Number, "Sales invoice issued",
            JournalEntrySource.SalesInvoice, invoice.Id);

        entry.AddLine(accounts[GeneralLedgerAccountCodes.AccountsReceivable], invoice.Total, 0, $"AR for {invoice.Number}");
        entry.AddLine(accounts[GeneralLedgerAccountCodes.SalesRevenue], 0, invoice.SubTotal, $"Revenue for {invoice.Number}");
        if (invoice.TaxTotal > 0)
            entry.AddLine(accounts[GeneralLedgerAccountCodes.SalesTaxPayable], 0, invoice.TaxTotal, $"Tax on {invoice.Number}");

        entry.Post();
        _db.JournalEntries.Add(entry);
    }

    public async Task PostSalesPaymentAsync(Payment payment, CancellationToken ct = default)
    {
        await AccountingPeriodGuard.EnsureOpenAsync(_db, payment.PaymentDate, ct);

        var accounts = await ResolveAsync(ct, GeneralLedgerAccountCodes.Cash, GeneralLedgerAccountCodes.AccountsReceivable);

        var entry = NewEntry(payment.PaymentDate, payment.Number, "Customer payment received",
            JournalEntrySource.Payment, payment.Id);

        entry.AddLine(accounts[GeneralLedgerAccountCodes.Cash], payment.Amount, 0, $"Payment {payment.Number}");
        entry.AddLine(accounts[GeneralLedgerAccountCodes.AccountsReceivable], 0, payment.Amount, $"Payment {payment.Number}");

        entry.Post();
        _db.JournalEntries.Add(entry);
    }

    public async Task PostSupplierInvoiceApprovedAsync(SupplierInvoice invoice, CancellationToken ct = default)
    {
        // Simplification: this MVP does not distinguish stocked-inventory lines from direct
        // expense lines on a supplier invoice, so the whole bill (incl. input tax) debits
        // Inventory. A future iteration could inspect each line's product to post COGS/expense
        // accounts per line as the plan's "Dr COGS/Inventory per line" describes.
        await AccountingPeriodGuard.EnsureOpenAsync(_db, invoice.IssueDate, ct);

        var accounts = await ResolveAsync(ct, GeneralLedgerAccountCodes.Inventory, GeneralLedgerAccountCodes.AccountsPayable);

        var entry = NewEntry(invoice.IssueDate, invoice.Number, "Supplier invoice approved",
            JournalEntrySource.SupplierInvoice, invoice.Id);

        entry.AddLine(accounts[GeneralLedgerAccountCodes.Inventory], invoice.Total, 0, $"Inventory for {invoice.Number}");
        entry.AddLine(accounts[GeneralLedgerAccountCodes.AccountsPayable], 0, invoice.Total, $"AP for {invoice.Number}");

        entry.Post();
        _db.JournalEntries.Add(entry);
    }

    public async Task PostSupplierPaymentAsync(SupplierPayment payment, CancellationToken ct = default)
    {
        await AccountingPeriodGuard.EnsureOpenAsync(_db, payment.PaymentDate, ct);

        var accounts = await ResolveAsync(ct, GeneralLedgerAccountCodes.AccountsPayable, GeneralLedgerAccountCodes.Cash);

        var entry = NewEntry(payment.PaymentDate, payment.Number, "Supplier payment made",
            JournalEntrySource.Payment, payment.Id);

        entry.AddLine(accounts[GeneralLedgerAccountCodes.AccountsPayable], payment.Amount, 0, $"Payment {payment.Number}");
        entry.AddLine(accounts[GeneralLedgerAccountCodes.Cash], 0, payment.Amount, $"Payment {payment.Number}");

        entry.Post();
        _db.JournalEntries.Add(entry);
    }

    private static JournalEntry NewEntry(
        DateOnly entryDate, string reference, string description, JournalEntrySource source, Guid sourceDocumentId)
        => new()
        {
            EntryDate = entryDate,
            Reference = reference,
            Description = description,
            Source = source,
            SourceDocumentId = sourceDocumentId
        };

    private async Task<Dictionary<string, Guid>> ResolveAsync(CancellationToken ct, params string[] codes)
    {
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);

        foreach (var code in codes)
            if (!accounts.ContainsKey(code))
                throw new InvalidOperationException(
                    $"System account {code} is missing. The chart of accounts seed must run before posting.");

        return accounts;
    }
}
