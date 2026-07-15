using ERP.Domain.Common;

namespace ERP.Domain.Accounting;

/// <summary>
/// A single ledger account in the chart of accounts. System accounts (<see cref="IsSystem"/>)
/// are seeded so auto-posting always has somewhere to post to and can never be deleted, only
/// deactivated. Balances are never cached here — they are always computed by summing
/// <see cref="Accounting.JournalLine"/> rows, so they can never drift from the ledger.
/// </summary>
public class Account : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }

    /// <summary>Seeded accounts the auto-posting handlers rely on; cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("Account is already inactive.");
        IsActive = false;
    }

    public void Activate() => IsActive = true;
}
