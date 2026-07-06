using ERP.Domain.Common;
using ERP.Domain.Taxation;

namespace ERP.Domain.Catalog;

/// <summary>
/// A sellable/stockable product (item master). References the shared vocabulary —
/// category, unit of measure, and tax — that sales, purchasing, and inventory build on.
/// Per-warehouse stock levels live in the inventory module, not here.
/// </summary>
public class Product : AuditableEntity
{
    /// <summary>Stock-keeping unit — the business identifier, unique across products.</summary>
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Barcode { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    /// <summary>Tax applied when this product is sold (drives VAT on invoices).</summary>
    public Guid TaxId { get; set; }
    public Tax? Tax { get; set; }

    /// <summary>Default cost and selling prices in base currency; transactional prices may override.</summary>
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }

    public bool IsActive { get; set; } = true;
}
