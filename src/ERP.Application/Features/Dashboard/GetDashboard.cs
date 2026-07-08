using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Sales;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Dashboard;

/// <summary>Headline figures for the home dashboard.</summary>
public sealed record DashboardDto(
    int CustomerCount,
    int SupplierCount,
    int ProductCount,
    decimal InventoryValue,
    int LowStockCount,
    decimal SalesThisMonth,
    decimal AccountsReceivable,
    decimal AccountsPayable,
    int OpenSalesOrders,
    int OpenPurchaseOrders);

public sealed record GetDashboardQuery : IRequest<Result<DashboardDto>>;

public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;
    private readonly IBranchScope _scope;

    public GetDashboardQueryHandler(IApplicationDbContext db, IDateTime clock, IBranchScope scope)
    {
        _db = db;
        _clock = clock;
        _scope = scope;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var customerCount = await _db.Customers.CountAsync(ct);
        var supplierCount = await _db.Suppliers.CountAsync(ct);
        var productCount = await _db.Products.CountAsync(ct);

        // Record-level security: stock and orders are reported only for the caller's branch.
        var allowed = await _scope.AllowedWarehouseIdsAsync(ct);

        var items = _db.InventoryItems.AsNoTracking();
        var layers = _db.StockLayers.AsNoTracking();
        var salesOrders = _db.SalesOrders.AsNoTracking();
        var purchaseOrders = _db.PurchaseOrders.AsNoTracking();

        if (allowed is not null)
        {
            var scopedItemIds = items.Where(i => allowed.Contains(i.WarehouseId)).Select(i => i.Id);
            items = items.Where(i => allowed.Contains(i.WarehouseId));
            layers = layers.Where(l => scopedItemIds.Contains(l.InventoryItemId));
            salesOrders = salesOrders.Where(o => allowed.Contains(o.WarehouseId));
            purchaseOrders = purchaseOrders.Where(o => allowed.Contains(o.WarehouseId));
        }

        var inventoryValue = await layers.SumAsync(l => (decimal?)(l.RemainingQuantity * l.UnitCost), ct) ?? 0m;
        var lowStockCount = await items.CountAsync(i => i.QuantityOnHand <= i.ReorderLevel, ct);

        var openSalesOrders = await salesOrders.CountAsync(
            o => o.Status == SalesOrderStatus.Confirmed, ct);
        var openPurchaseOrders = await purchaseOrders.CountAsync(
            o => o.Status == PurchaseOrderStatus.Confirmed || o.Status == PurchaseOrderStatus.PartiallyReceived, ct);

        // Invoice/supplier-invoice totals derive from lines, so load the relevant open
        // documents with their lines and total in memory.
        var arInvoices = await _db.Invoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .ToListAsync(ct);
        var accountsReceivable = arInvoices.Sum(i => i.Balance);

        var salesInvoices = await _db.Invoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => i.IssueDate >= monthStart
                        && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Paid))
            .ToListAsync(ct);
        var salesThisMonth = salesInvoices.Sum(i => i.Total);

        var apInvoices = await _db.SupplierInvoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => i.Status == SupplierInvoiceStatus.Approved || i.Status == SupplierInvoiceStatus.PartiallyPaid)
            .ToListAsync(ct);
        var accountsPayable = apInvoices.Sum(i => i.Balance);

        return Result.Success(new DashboardDto(
            customerCount, supplierCount, productCount, inventoryValue, lowStockCount,
            salesThisMonth, accountsReceivable, accountsPayable, openSalesOrders, openPurchaseOrders));
    }
}
