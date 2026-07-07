using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Dashboard;

public sealed record SalesTrendPoint(string Month, string Label, decimal Total);

/// <summary>Monthly invoiced sales over the trailing <paramref name="Months"/> (default 6), oldest first.</summary>
public sealed record GetSalesTrendQuery(int Months = 6) : IRequest<Result<IReadOnlyList<SalesTrendPoint>>>;

public sealed class GetSalesTrendQueryHandler
    : IRequestHandler<GetSalesTrendQuery, Result<IReadOnlyList<SalesTrendPoint>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public GetSalesTrendQueryHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<SalesTrendPoint>>> Handle(GetSalesTrendQuery request, CancellationToken ct)
    {
        var months = Math.Clamp(request.Months, 1, 24);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        // Invoices that count as sales; total derives from lines, so load with lines and sum in memory.
        var invoices = await _db.Invoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => i.IssueDate >= firstMonth
                        && (i.Status == InvoiceStatus.Issued
                            || i.Status == InvoiceStatus.PartiallyPaid
                            || i.Status == InvoiceStatus.Paid))
            .ToListAsync(ct);

        var byMonth = invoices
            .GroupBy(i => new DateOnly(i.IssueDate.Year, i.IssueDate.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Total));

        // Emit a continuous series (zero-filled) so the chart has no gaps.
        var points = new List<SalesTrendPoint>(months);
        for (var m = firstMonth; m <= today; m = m.AddMonths(1))
        {
            byMonth.TryGetValue(m, out var total);
            points.Add(new SalesTrendPoint(
                m.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                m.ToString("MMM", CultureInfo.InvariantCulture),
                total));
        }

        return Result.Success<IReadOnlyList<SalesTrendPoint>>(points);
    }
}
