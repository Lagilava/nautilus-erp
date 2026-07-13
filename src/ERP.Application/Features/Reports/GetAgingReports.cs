using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reports;

/// <summary>Accounts-receivable aging: outstanding customer invoice balances bucketed by days overdue.</summary>
public sealed record GetReceivablesAgingReportQuery : IRequest<Result<ReportTable>>;

/// <summary>Accounts-payable aging: outstanding supplier invoice balances bucketed by days overdue.</summary>
public sealed record GetPayablesAgingReportQuery : IRequest<Result<ReportTable>>;

/// <summary>
/// Shared aging arithmetic. An invoice's balance lands in one bucket based on how far past
/// its due date it is today (invoices without a due date age from their issue date). Totals
/// derive from lines, which are not stored as columns, so the outstanding documents are
/// loaded with their lines and bucketed in memory — outstanding-invoice counts are small
/// relative to the table, and this keeps the arithmetic identical to the domain model's.
/// </summary>
internal static class Aging
{
    public static readonly string[] BucketNames = { "Current", "1–30 days", "31–60 days", "61–90 days", "90+ days" };

    public static int BucketIndex(DateOnly due, DateOnly today)
    {
        var daysOverdue = today.DayNumber - due.DayNumber;
        return daysOverdue switch
        {
            <= 0 => 0,
            <= 30 => 1,
            <= 60 => 2,
            <= 90 => 3,
            _ => 4
        };
    }

    /// <summary>Builds the report table from per-party bucketed balances.</summary>
    public static ReportTable Build(string title, string partyHeader,
        IEnumerable<(string Party, DateOnly Due, decimal Balance)> items)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var byParty = items
            .GroupBy(x => x.Party)
            .Select(g =>
            {
                var buckets = new decimal[5];
                foreach (var (_, due, balance) in g)
                    buckets[BucketIndex(due, today)] += balance;
                return (Party: g.Key, Buckets: buckets, Total: buckets.Sum());
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        var ci = CultureInfo.InvariantCulture;
        var rows = byParty
            .Select(r => (IReadOnlyList<string>)new[] { r.Party }
                .Concat(r.Buckets.Select(b => b.ToString("0.00", ci)))
                .Append(r.Total.ToString("0.00", ci))
                .ToArray())
            .ToList();

        var grand = new decimal[5];
        foreach (var r in byParty)
            for (var i = 0; i < 5; i++)
                grand[i] += r.Buckets[i];
        rows.Add(new[] { "TOTAL" }
            .Concat(grand.Select(b => b.ToString("0.00", ci)))
            .Append(grand.Sum().ToString("0.00", ci))
            .ToArray());

        var headers = new[] { partyHeader }.Concat(BucketNames).Append("Total").ToArray();
        return new ReportTable(title, headers, rows);
    }
}

public sealed class GetReceivablesAgingReportQueryHandler
    : IRequestHandler<GetReceivablesAgingReportQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;

    public GetReceivablesAgingReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetReceivablesAgingReportQuery request, CancellationToken ct)
    {
        // Loaded with lines (totals are line-derived), names resolved separately: an Include
        // combined with a Join projection is not guaranteed to materialize the collection.
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .Include(i => i.Lines)
            .ToListAsync(ct);

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var names = await _db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var items = invoices
            .Where(i => i.Balance > 0)
            .Select(i => (names.GetValueOrDefault(i.CustomerId, "(unknown)"),
                i.DueDate ?? i.IssueDate, i.Balance));

        return Result.Success(Aging.Build("Receivables Aging", "Customer", items));
    }
}

public sealed class GetPayablesAgingReportQueryHandler
    : IRequestHandler<GetPayablesAgingReportQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;

    public GetPayablesAgingReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetPayablesAgingReportQuery request, CancellationToken ct)
    {
        var invoices = await _db.SupplierInvoices.AsNoTracking()
            .Where(i => i.Status == SupplierInvoiceStatus.Approved
                        || i.Status == SupplierInvoiceStatus.PartiallyPaid)
            .Include(i => i.Lines)
            .ToListAsync(ct);

        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        var names = await _db.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var items = invoices
            .Where(i => i.Balance > 0)
            .Select(i => (names.GetValueOrDefault(i.SupplierId, "(unknown)"),
                i.DueDate ?? i.IssueDate, i.Balance));

        return Result.Success(Aging.Build("Payables Aging", "Supplier", items));
    }
}
