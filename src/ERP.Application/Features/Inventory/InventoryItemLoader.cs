using ERP.Application.Common.Interfaces;
using ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Inventory;

/// <summary>
/// Loads an <see cref="InventoryItem"/> (with its FIFO layers) for a product/warehouse,
/// creating a fresh, empty one if none exists yet. Shared by the stock command handlers so
/// the get-or-create and eager-load of layers is written once.
/// </summary>
internal static class InventoryItemLoader
{
    public static async Task<InventoryItem> GetOrCreateAsync(
        IApplicationDbContext db, Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var item = await db.InventoryItems
            .Include(i => i.Layers)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId, ct);

        if (item is null)
        {
            item = new InventoryItem { ProductId = productId, WarehouseId = warehouseId };
            db.InventoryItems.Add(item);
        }

        return item;
    }
}
