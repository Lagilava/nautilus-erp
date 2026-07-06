using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reports;

/// <summary>Produces the inventory-valuation report as a provider-agnostic <see cref="ReportTable"/>.</summary>
public sealed record GetInventoryValuationReportQuery(Guid? WarehouseId = null) : IRequest<Result<ReportTable>>;

public sealed class GetInventoryValuationReportQueryHandler
    : IRequestHandler<GetInventoryValuationReportQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;
    public GetInventoryValuationReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetInventoryValuationReportQuery request, CancellationToken ct)
    {
        var query = _db.InventoryItems.AsNoTracking();
        if (request.WarehouseId is { } wh) query = query.Where(i => i.WarehouseId == wh);

        var rows = await query
            .Join(_db.Products, i => i.ProductId, p => p.Id, (i, p) => new { i, p })
            .Join(_db.Warehouses, x => x.i.WarehouseId, w => w.Id, (x, w) => new
            {
                x.p.Sku,
                ProductName = x.p.Name,
                Warehouse = w.Name,
                x.i.QuantityOnHand,
                Value = x.i.Layers.Sum(l => l.RemainingQuantity * l.UnitCost)
            })
            .OrderBy(r => r.Warehouse).ThenBy(r => r.ProductName)
            .ToListAsync(ct);

        var ci = CultureInfo.InvariantCulture;
        var tableRows = rows
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.Sku, r.ProductName, r.Warehouse,
                r.QuantityOnHand.ToString("0.####", ci), r.Value.ToString("0.00", ci)
            })
            .ToList();

        // Total row.
        tableRows.Add(new[] { "", "", "TOTAL", "", rows.Sum(r => r.Value).ToString("0.00", ci) });

        var table = new ReportTable(
            "Inventory Valuation",
            new[] { "SKU", "Product", "Warehouse", "Qty On Hand", "Value" },
            tableRows);

        return Result.Success(table);
    }
}
