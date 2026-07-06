using ERP.Domain.Common;

namespace ERP.Domain.Catalog;

/// <summary>
/// A product category, optionally nested (self-referencing) to form a hierarchy such as
/// Grocery → Beverages → Soft Drinks.
/// </summary>
public class Category : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Parent in the hierarchy; null for a top-level category.</summary>
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public bool IsActive { get; set; } = true;
}
