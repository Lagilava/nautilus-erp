using ERP.Domain.Accounting;
using ERP.Domain.Auditing;
using ERP.Domain.Catalog;
using ERP.Domain.Documents;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Domain.Taxation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    // Sales (Milestone 5).
    DbSet<Customer> Customers { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Payment> Payments { get; }

    // Purchasing (Milestone 6).
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<SupplierInvoice> SupplierInvoices { get; }
    DbSet<SupplierInvoiceLine> SupplierInvoiceLines { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }

    // Auditing (Milestone 7).
    DbSet<AuditLog> AuditLogs { get; }

    // Company identity (for compliant tax invoices).
    DbSet<CompanyProfile> CompanyProfiles { get; }

    DbSet<Attachment> Attachments { get; }

    // Accounting / General Ledger (Milestone 12).
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }
    DbSet<Reconciliation> Reconciliations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Exposes the change tracker entry for a loaded entity — needed to set an
    /// original concurrency-token value before a conditional update.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
