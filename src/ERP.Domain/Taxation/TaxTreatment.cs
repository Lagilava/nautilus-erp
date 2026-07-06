namespace ERP.Domain.Taxation;

/// <summary>
/// How a tax applies, per Fiji VAT rules. Standard carries a positive rate; zero-rated
/// is taxable at 0% (still reportable); exempt is outside VAT entirely. The distinction
/// matters for FRCS/VMS reporting, so it is modelled explicitly rather than as "rate = 0".
/// </summary>
public enum TaxTreatment
{
    Standard = 1,
    ZeroRated = 2,
    Exempt = 3
}
