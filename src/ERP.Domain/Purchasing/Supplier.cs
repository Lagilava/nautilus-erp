using ERP.Domain.Common;

namespace ERP.Domain.Purchasing;

/// <summary>A party the business buys from. Referenced by purchase orders and supplier invoices.</summary>
public class Supplier : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>FRCS taxpayer identification number of the supplier, where applicable.</summary>
    public string? TaxIdentificationNumber { get; set; }

    public bool IsActive { get; set; } = true;
}
