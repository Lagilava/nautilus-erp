using ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Inventory;

public sealed class InventoryItemConfiguration : AuditableEntityConfiguration<InventoryItem>
{
    public override void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("InventoryItems");

        builder.Property(x => x.QuantityOnHand).HasPrecision(18, 4);
        builder.Property(x => x.ReorderLevel).HasPrecision(18, 4);

        // One stock record per product per warehouse.
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();

        builder.HasMany(x => x.Layers)
            .WithOne()
            .HasForeignKey(l => l.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockLayerConfiguration : IEntityTypeConfiguration<StockLayer>
{
    public void Configure(EntityTypeBuilder<StockLayer> builder)
    {
        builder.ToTable("StockLayers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.OriginalQuantity).HasPrecision(18, 4);
        builder.Property(x => x.RemainingQuantity).HasPrecision(18, 4);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.InventoryItemId, x.SequenceNumber });
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class StockMovementConfiguration : AuditableEntityConfiguration<StockMovement>
{
    public override void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        base.Configure(builder);
        builder.ToTable("StockMovements");

        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.TotalCost).HasPrecision(18, 4);
        builder.Property(x => x.Reference).HasMaxLength(64);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => new { x.ProductId, x.WarehouseId, x.OccurredAt });
    }
}
