using System.Reflection;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Sales;
using ERP.Domain.Taxation;
using ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ERP.Persistence;

/// <summary>
/// The application's EF Core context. Also the ASP.NET Identity store. Implements
/// <see cref="IApplicationDbContext"/> so the Application layer depends on the abstraction.
/// Audit stamping and soft-delete conversion happen centrally in <see cref="SaveChangesAsync"/>.
/// </summary>
public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser,
        IDateTime clock) : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockLayer> StockLayers => Set<StockLayer>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Picks up every IEntityTypeConfiguration<T> in this assembly (incl. soft-delete filters).
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Our entities assign their own Guid PK in code (BaseEntity). Without this, EF treats
        // the key as store-generated: a new entity added to an already-tracked parent's
        // collection has a non-default key, so EF misclassifies it as Modified (an UPDATE of a
        // non-existent row) instead of Added. Mark BaseEntity Guid keys as never store-generated.
        // Identity's own types (ApplicationUser/Role) are left untouched — they don't self-assign.
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
        {
            var key = entityType.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                key.Properties[0].ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps audit columns and converts hard deletes of auditable entities into soft
    /// deletes, so the global query filter hides them while the row is retained.
    /// </summary>
    private void StampAuditFields()
    {
        var now = _clock.UtcNow;
        var user = _currentUser.UserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.ModifiedBy = user;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.ModifiedBy = user;
                    break;
            }
        }
    }
}
