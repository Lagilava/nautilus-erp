using ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Purchasing;

public sealed class SupplierConfiguration : AuditableEntityConfiguration<Supplier>
{
    public override void Configure(EntityTypeBuilder<Supplier> builder)
    {
        base.Configure(builder);
        builder.ToTable("Suppliers");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(32);
    }
}

public sealed class PurchaseOrderConfiguration : AuditableEntityConfiguration<PurchaseOrder>
{
    public override void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        base.Configure(builder);
        builder.ToTable("PurchaseOrders");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.SupplierId);
        builder.Ignore(x => x.SubTotal);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.QuantityReceived).HasPrecision(18, 4);
        builder.Ignore(x => x.OutstandingQuantity);
        builder.Ignore(x => x.LineTotal);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class GoodsReceiptConfiguration : AuditableEntityConfiguration<GoodsReceipt>
{
    public override void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        base.Configure(builder);
        builder.ToTable("GoodsReceipts");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.PurchaseOrderId);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("GoodsReceiptLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class SupplierInvoiceConfiguration : AuditableEntityConfiguration<SupplierInvoice>
{
    public override void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        base.Configure(builder);
        builder.ToTable("SupplierInvoices");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.SupplierReference).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.HasIndex(x => x.SupplierId);
        builder.Ignore(x => x.SubTotal);
        builder.Ignore(x => x.TaxTotal);
        builder.Ignore(x => x.Total);
        builder.Ignore(x => x.Balance);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.SupplierInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable("SupplierInvoiceLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.TaxRate).HasPrecision(9, 4);
        builder.Ignore(x => x.LineSubTotal);
        builder.Ignore(x => x.LineTax);
        builder.Ignore(x => x.LineTotal);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class SupplierPaymentConfiguration : AuditableEntityConfiguration<SupplierPayment>
{
    public override void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        base.Configure(builder);
        builder.ToTable("SupplierPayments");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Method).HasConversion<int>();
        builder.Property(x => x.Reference).HasMaxLength(128);
        builder.HasIndex(x => x.SupplierInvoiceId);
    }
}
