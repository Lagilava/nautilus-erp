using System.Globalization;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Reporting;
using ERP.Domain.Accounting;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Reports;

/// <summary>
/// Profit and loss over a date range: posted Revenue journal lines less posted Expense
/// journal lines, grouped by account. Same shape as <c>GetTrialBalance.cs</c>.
/// </summary>
public sealed record GetProfitAndLossQuery(DateOnly FromDate, DateOnly ToDate, Guid? BranchId = null)
    : IRequest<Result<ReportTable>>;

public sealed class GetProfitAndLossQueryHandler : IRequestHandler<GetProfitAndLossQuery, Result<ReportTable>>
{
    private readonly IApplicationDbContext _db;
    public GetProfitAndLossQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ReportTable>> Handle(GetProfitAndLossQuery request, CancellationToken ct)
    {
        var query = _db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Posted
                        && e.EntryDate >= request.FromDate && e.EntryDate <= request.ToDate);

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
            .Where(a => accountIds.Contains(a.Id)
                        && (a.Type == AccountType.Revenue || a.Type == AccountType.Expense))
            .ToDictionaryAsync(a => a.Id, ct);

        var ci = CultureInfo.InvariantCulture;
        var revenueRows = new List<(string Code, string Name, decimal Amount)>();
        var expenseRows = new List<(string Code, string Name, decimal Amount)>();

        foreach (var g in grouped)
        {
            if (!accounts.TryGetValue(g.AccountId, out var account)) continue;

            // Revenue accounts carry a natural credit balance, expenses a natural debit balance.
            if (account.Type == AccountType.Revenue)
                revenueRows.Add((account.Code, account.Name, g.Credit - g.Debit));
            else
                expenseRows.Add((account.Code, account.Name, g.Debit - g.Credit));
        }

        revenueRows = revenueRows.OrderBy(r => r.Code).ToList();
        expenseRows = expenseRows.OrderBy(r => r.Code).ToList();

        var totalRevenue = revenueRows.Sum(r => r.Amount);
        var totalExpense = expenseRows.Sum(r => r.Amount);
        var netIncome = totalRevenue - totalExpense;

        var rows = new List<IReadOnlyList<string>>();
        rows.Add(new[] { "", "REVENUE", "" });
        rows.AddRange(revenueRows.Select(r => (IReadOnlyList<string>)new[] { r.Code, r.Name, r.Amount.ToString("0.00", ci) }));
        rows.Add(new[] { "", "Total Revenue", totalRevenue.ToString("0.00", ci) });
        rows.Add(new[] { "", "EXPENSES", "" });
        rows.AddRange(expenseRows.Select(r => (IReadOnlyList<string>)new[] { r.Code, r.Name, r.Amount.ToString("0.00", ci) }));
        rows.Add(new[] { "", "Total Expenses", totalExpense.ToString("0.00", ci) });
        rows.Add(new[] { "", "NET INCOME", netIncome.ToString("0.00", ci) });

        var table = new ReportTable("Profit and Loss", new[] { "Code", "Account", "Amount" }, rows);
        return Result.Success(table);
    }
}
