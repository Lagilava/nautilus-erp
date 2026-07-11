using System.Reflection;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auditing;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Documents;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Domain.Taxation;
using ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // Attach the audit interceptor here (rather than at registration) so it applies to
        // every provider — including the in-memory provider used by integration tests —
        // and gets the request-scoped services already injected into this context.
        optionsBuilder.AddInterceptors(new Auditing.AuditSaveChangesInterceptor(_currentUser, _clock));
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

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

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

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

        // SQLite refuses to ORDER BY a DateTimeOffset (its TEXT form doesn't sort correctly across
        // offsets), which would break the stock ledger and audit trail. Every timestamp we store is
        // UTC, so persist them as UTC ticks under SQLite: sortable, lossless, and query code stays
        // identical across providers. SQL Server keeps native datetimeoffset.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var toTicks = new ValueConverter<DateTimeOffset, long>(
                d => d.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            var toTicksNullable = new ValueConverter<DateTimeOffset?, long?>(
                d => d.HasValue ? d.Value.UtcTicks : null,
                v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

            foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(toTicks);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(toTicksNullable);
            }
        }

        // Only SQL Server auto-generates rowversion values. Under SQLite (local dev), Postgres,
        // or the in-memory provider (tests) the token stays null, so EF's `WHERE RowVersion =
        // @orig` concurrency check would never match and updates would fail. Neutralise the
        // tokens for those providers; SQL Server keeps real optimistic concurrency.
        //
        // KNOWN LIMITATION: this means a Postgres deployment has no optimistic concurrency —
        // two users editing the same record last-write-wins instead of one getting a conflict.
        // The fix is Npgsql's system `xmin` column (UseXminAsConcurrencyToken), which needs a
        // per-entity mapping rather than this blanket sweep.
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
        {
            foreach (var property in builder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetProperties())
                         .Where(p => p.IsConcurrencyToken))
            {
                property.IsConcurrencyToken = false;
                property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
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
