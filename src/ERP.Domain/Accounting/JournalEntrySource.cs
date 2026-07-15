namespace ERP.Domain.Accounting;

/// <summary>What raised a journal entry — a human (Manual) or an existing business event
/// that auto-posts through <c>IGeneralLedgerPoster</c>.</summary>
public enum JournalEntrySource
{
    Manual = 1,
    SalesInvoice = 2,
    SupplierInvoice = 3,
    Payment = 4
}
