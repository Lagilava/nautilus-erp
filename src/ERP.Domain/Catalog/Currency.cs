using ERP.Domain.Common;

namespace ERP.Domain.Catalog;

/// <summary>
/// An ISO 4217 currency. Exactly one currency is the base currency (FJD for Fiji);
/// all monetary reporting is ultimately expressed in it. Effective-dated exchange
/// rates are introduced with the financial module.
/// </summary>
public class Currency : AuditableEntity
{
    /// <summary>ISO 4217 alphabetic code, e.g. "FJD", "USD", "AUD".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }

    /// <summary>Whether this is the system base currency. Enforced unique in configuration.</summary>
    public bool IsBaseCurrency { get; set; }

    public bool IsActive { get; set; } = true;
}
