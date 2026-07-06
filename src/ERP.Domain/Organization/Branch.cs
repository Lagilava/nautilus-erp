using ERP.Domain.Common;

namespace ERP.Domain.Organization;

/// <summary>
/// A physical business location (store/office). Warehouses and, later, sales/purchases
/// are attributed to a branch.
/// </summary>
public class Branch : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>FRCS taxpayer identification (TIN) for this location, where applicable.</summary>
    public string? TaxIdentificationNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}
