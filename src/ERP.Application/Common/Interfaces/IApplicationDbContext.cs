using ERP.Domain.Catalog;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Taxation;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the Application layer is allowed to see. Handlers depend
/// on this abstraction, not on the concrete DbContext, keeping them testable and the
/// dependency direction inward. Aggregate roots are exposed as sets are needed.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<LoginHistory> LoginHistories { get; }

    // Reference data (Milestone 3).
    DbSet<Currency> Currencies { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Category> Categories { get; }
    DbSet<Tax> Taxes { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }

    // Inventory (Milestone 4).
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<StockLayer> StockLayers { get; }
    DbSet<StockMovement> StockMovements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
