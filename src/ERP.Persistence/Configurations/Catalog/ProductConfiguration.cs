using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Catalog;

public sealed class ProductConfiguration : AuditableEntityConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);
        builder.ToTable("Products");

        builder.Property(x => x.Sku).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Barcode).HasMaxLength(64);
        builder.HasIndex(x => x.Barcode);

        builder.Property(x => x.CostPrice).HasPrecision(18, 4);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 4);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tax)
            .WithMany()
            .HasForeignKey(x => x.TaxId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
