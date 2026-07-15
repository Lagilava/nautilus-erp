using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common.Services;

/// <summary>
/// Shared check used by both manual journal-entry creation/posting and
/// <see cref="IGeneralLedgerPoster"/>'s auto-posting: an entry may never be dated inside a
/// closed <c>AccountingPeriod</c>. Throws <see cref="DomainException"/> so callers translate
/// it into a <c>Result.Failure(Error.Conflict(...))</c> the same way other domain guards do.
/// </summary>
public static class AccountingPeriodGuard
{
    public static async Task EnsureOpenAsync(IApplicationDbContext db, DateOnly entryDate, CancellationToken ct = default)
    {
        var period = await db.AccountingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Year == entryDate.Year && p.Month == entryDate.Month, ct);

        if (period is { IsClosed: true })
            throw new DomainException(
                $"The accounting period {entryDate.Year}-{entryDate.Month:D2} is closed. Choose a date in an open period.");
    }
}
