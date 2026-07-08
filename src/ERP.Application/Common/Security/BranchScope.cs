using ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common.Security;

/// <summary>
/// Resolves the record-level scope of the current caller. Warehouse-bound data (stock,
/// sales orders, purchase orders) belongs to a branch through its warehouse, so a
/// branch-scoped user may only see records for warehouses in their branch.
/// </summary>
public interface IBranchScope
{
    /// <summary>
    /// Warehouse ids the caller may see, or <c>null</c> when unrestricted (no branch claim).
    /// An empty set means the caller's branch has no warehouses — they see nothing.
    /// </summary>
    Task<IReadOnlyCollection<Guid>?> AllowedWarehouseIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the caller may act on the given warehouse.</summary>
    Task<bool> CanAccessWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default);
}

internal sealed class BranchScope : IBranchScope
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BranchScope(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<Guid>?> AllowedWarehouseIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.BranchId is not { } branchId)
            return null; // unrestricted

        return await _db.Warehouses.AsNoTracking()
            .Where(w => w.BranchId == branchId)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CanAccessWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.BranchId is not { } branchId)
            return true;

        return await _db.Warehouses.AsNoTracking()
            .AnyAsync(w => w.Id == warehouseId && w.BranchId == branchId, cancellationToken);
    }
}
