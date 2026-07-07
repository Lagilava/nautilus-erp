using ERP.Domain.Common;

namespace ERP.Domain.Organization;

/// <summary>
/// The business's own identity — the seller details a compliant Fiji tax invoice must carry
/// (legal name + FRCS TIN + address). A single row; the well-known <see cref="SingletonId"/>
/// keeps it to one record.
/// </summary>
public class CompanyProfile : AuditableEntity
{
    public static readonly Guid SingletonId = new("11111111-1111-1111-1111-111111111111");

    public string LegalName { get; set; } = "Your Company Ltd";
    public string? TradingName { get; set; }

    /// <summary>FRCS Taxpayer Identification Number — printed on every tax invoice.</summary>
    public string? Tin { get; set; }

    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; } = "Fiji";
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Base currency ISO code; Fiji dollars by default.</summary>
    public string BaseCurrency { get; set; } = "FJD";
}
