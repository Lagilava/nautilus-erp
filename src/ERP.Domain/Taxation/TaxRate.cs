using ERP.Domain.Common;

namespace ERP.Domain.Taxation;

/// <summary>
/// An effective-dated percentage for a <see cref="Tax"/>. Overlapping windows for the same
/// tax are invalid (enforced by the application layer). An open-ended window (<see
/// cref="EffectiveTo"/> null) is the current rate.
/// </summary>
public class TaxRate : AuditableEntity
{
    public Guid TaxId { get; set; }
    public Tax? Tax { get; set; }

    /// <summary>Percentage, e.g. 15.00 for 15% VAT.</summary>
    public decimal Percentage { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
