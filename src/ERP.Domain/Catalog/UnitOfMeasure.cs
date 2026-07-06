using ERP.Domain.Common;

namespace ERP.Domain.Catalog;

/// <summary>
/// A unit in which a product is counted, bought, or sold (e.g. EA, KG, BOX, LITRE).
/// </summary>
public class UnitOfMeasure : AuditableEntity
{
    /// <summary>Short code, e.g. "EA", "KG".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
