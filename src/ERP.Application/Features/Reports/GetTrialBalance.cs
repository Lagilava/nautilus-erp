using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reports;

/// <summary>
/// Trial balance: every posted journal line, grouped by account, with debit/credit totals —
/// the foundation report every other financial report builds on. Follows
/// <c>GetAgingReports.cs</c>'s shape: a query producing a provider-agnostic <see cref="ReportTable"/>.
/// </summary>
public sealed record GetTrialBalanceQuery(Guid? BranchId = null, DateOnly? AsOfDate = null)
    : IRequest<Result<ReportTable>>;

public sealed class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;
    public GetTrialBalanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetTrialBalanceQuery request, CancellationToken ct)
    {
        var lines = _db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Posted);

        if (request.BranchId is { } branchId)
            lines = lines.Where(e => e.BranchId == branchId);
        if (request.AsOfDate is { } asOf)
            lines = lines.Where(e => e.EntryDate <= asOf);

        // Convert each line to base currency using its historical rate (rate 1 / null for
        // base-currency lines, so pre-multi-currency behavior is unchanged) before summing.
        var grouped = await lines
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(l => l.Debit * (l.ExchangeRate ?? 1m)),
                Credit = g.Sum(l => l.Credit * (l.ExchangeRate ?? 1m))
            })
            .ToListAsync(ct);

        var accountIds = grouped.Select(g => g.AccountId).ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var ci = CultureInfo.InvariantCulture;
        var rows = grouped
            .Select(g =>
            {
                var account = accounts.GetValueOrDefault(g.AccountId);
                return (Code: account?.Code ?? "?", Name: account?.Name ?? "(unknown)",
                    Type: account?.Type.ToString() ?? "?", g.Debit, g.Credit);
            })
            .OrderBy(r => r.Code)
            .ToList();

        var rowsFormatted = rows
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.Code, r.Name, r.Type, r.Debit.ToString("0.00", ci), r.Credit.ToString("0.00", ci)
            })
            .ToList();

        var totalDebit = rows.Sum(r => r.Debit);
        var totalCredit = rows.Sum(r => r.Credit);
        rowsFormatted.Add(new[] { "", "TOTAL", "", totalDebit.ToString("0.00", ci), totalCredit.ToString("0.00", ci) });

        var table = new ReportTable(
            "Trial Balance",
            new[] { "Code", "Account", "Type", "Debit", "Credit" },
            rowsFormatted);

        return Result.Success(table);
    }
}
