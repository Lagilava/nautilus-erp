namespace ERP.Application.Features.Catalog.Products;

/// <summary>Read model for a product, flattened with the display names of its references.</summary>
public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId,
    string CategoryName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    Guid TaxId,
    string TaxCode,
    decimal CostPrice,
    decimal SellingPrice,
    bool IsActive);
