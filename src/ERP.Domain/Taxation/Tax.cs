using ERP.Domain.Common;

namespace ERP.Domain.Taxation;

/// <summary>
/// A tax definition (e.g. "Fiji VAT"). Its percentage is <b>data, not code</b>: rates live
/// in <see cref="TaxRate"/> children with effective-dating, so a rate change (e.g. VAT
/// 15% → 15.5%) is a data operation and historical documents keep the rate that applied
/// on their date. See the Fiji Localization Requirements in the build instructions.
/// </summary>
public class Tax : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public TaxTreatment Treatment { get; set; } = TaxTreatment.Standard;

    public bool IsActive { get; set; } = true;

    public ICollection<TaxRate> Rates { get; set; } = new List<TaxRate>();

    /// <summary>
    /// The percentage in force on <paramref name="onDate"/>: the rate whose effective
    /// window contains the date. Zero-rated and exempt taxes are always 0%.
    /// </summary>
    public decimal GetRateOn(DateOnly onDate)
    {
        if (Treatment != TaxTreatment.Standard)
            return 0m;

        var applicable = Rates
            .Where(r => r.EffectiveFrom <= onDate && (r.EffectiveTo == null || r.EffectiveTo >= onDate))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        return applicable?.Percentage ?? 0m;
    }
}
