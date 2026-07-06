using ERP.Domain.Common;

namespace ERP.Domain.Sales;

/// <summary>A party the business sells to. Referenced by sales orders and invoices.</summary>
public class Customer : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>FRCS taxpayer identification number, where the customer is VAT-registered.</summary>
    public string? TaxIdentificationNumber { get; set; }

    /// <summary>Credit limit in base currency (0 = cash-only / no credit).</summary>
    public decimal CreditLimit { get; set; }

    public bool IsActive { get; set; } = true;
}
