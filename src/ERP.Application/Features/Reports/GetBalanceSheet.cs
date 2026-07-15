using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reports;

/// <summary>
/// Balance sheet as of a date: posted Asset/Liability/Equity journal lines grouped by
/// account. Same shape as <c>GetTrialBalance.cs</c>.
/// </summary>
public sealed record GetBalanceSheetQuery(DateOnly AsOfDate, Guid? BranchId = null) : IRequest<Result<ReportTable>>;

public sealed class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;
    public GetBalanceSheetQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetBalanceSheetQuery request, CancellationToken ct)
    {
        var query = _db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Posted && e.EntryDate <= request.AsOfDate);

        if (request.BranchId is { } branchId)
            query = query.Where(e => e.BranchId == branchId);

        // Convert each line to base currency using its historical rate before summing.
        var grouped = await query
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
        var assets = new List<(string Code, string Name, decimal Amount)>();
        var liabilities = new List<(string Code, string Name, decimal Amount)>();
        var equity = new List<(string Code, string Name, decimal Amount)>();

        // Also fold retained earnings (net revenue - expense) into equity so the sheet balances.
        decimal netIncome = 0;

        foreach (var g in grouped)
        {
            if (!accounts.TryGetValue(g.AccountId, out var account)) continue;
            switch (account.Type)
            {
                case AccountType.Asset:
                    assets.Add((account.Code, account.Name, g.Debit - g.Credit));
                    break;
                case AccountType.Liability:
                    liabilities.Add((account.Code, account.Name, g.Credit - g.Debit));
                    break;
                case AccountType.Equity:
                    equity.Add((account.Code, account.Name, g.Credit - g.Debit));
                    break;
                case AccountType.Revenue:
                    netIncome += g.Credit - g.Debit;
                    break;
                case AccountType.Expense:
                    netIncome -= g.Debit - g.Credit;
                    break;
            }
        }

        assets = assets.OrderBy(r => r.Code).ToList();
        liabilities = liabilities.OrderBy(r => r.Code).ToList();
        equity = equity.OrderBy(r => r.Code).ToList();

        var totalAssets = assets.Sum(r => r.Amount);
        var totalLiabilities = liabilities.Sum(r => r.Amount);
        var totalEquity = equity.Sum(r => r.Amount) + netIncome;

        var rows = new List<IReadOnlyList<string>>();
        rows.Add(new[] { "", "ASSETS", "" });
        rows.AddRange(assets.Select(r => (IReadOnlyList<string>)new[] { r.Code, r.Name, r.Amount.ToString("0.00", ci) }));
        rows.Add(new[] { "", "Total Assets", totalAssets.ToString("0.00", ci) });
        rows.Add(new[] { "", "LIABILITIES", "" });
        rows.AddRange(liabilities.Select(r => (IReadOnlyList<string>)new[] { r.Code, r.Name, r.Amount.ToString("0.00", ci) }));
        rows.Add(new[] { "", "Total Liabilities", totalLiabilities.ToString("0.00", ci) });
        rows.Add(new[] { "", "EQUITY", "" });
        rows.AddRange(equity.Select(r => (IReadOnlyList<string>)new[] { r.Code, r.Name, r.Amount.ToString("0.00", ci) }));
        rows.Add(new[] { "", "Retained Earnings (current)", netIncome.ToString("0.00", ci) });
        rows.Add(new[] { "", "Total Equity", totalEquity.ToString("0.00", ci) });
        rows.Add(new[] { "", "Total Liabilities + Equity", (totalLiabilities + totalEquity).ToString("0.00", ci) });

        var table = new ReportTable("Balance Sheet", new[] { "Code", "Account", "Amount" }, rows);
        return Result.Success(table);
    }
}
