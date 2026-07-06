namespace ERP.Domain.Sales;

/// <summary>
/// Invoice lifecycle. Draft is editable; Issued is a committed tax document; payments move
/// it to PartiallyPaid then Paid; Void is terminal (only before any payment).
/// </summary>
public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Void = 5
}

/// <summary>
/// FRCS/VMS fiscalization state of an invoice. Deliberately explicit and defaulting to
/// NotSubmitted — the real VMS integration is unverified, so nothing is faked as Submitted.
/// </summary>
public enum FiscalStatus
{
    NotSubmitted = 0,
    Submitted = 1,
    Failed = 2
}

/// <summary>How a payment was tendered. Includes Fiji mobile wallets (e.g. M-PAiSA).</summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    MobileWallet = 4,
    Cheque = 5
}
