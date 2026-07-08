using ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Sales;

public sealed class CustomerConfiguration : AuditableEntityConfiguration<Customer>
{
    public override void Configure(EntityTypeBuilder<Customer> builder)
    {
        base.Configure(builder);
        builder.ToTable("Customers");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(32);
        builder.Property(x => x.CreditLimit).HasPrecision(18, 2);
    }
}

public sealed class SalesOrderConfiguration : AuditableEntityConfiguration<SalesOrder>
{
    public override void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        base.Configure(builder);
        builder.ToTable("SalesOrders");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.CustomerId);
        builder.Ignore(x => x.SubTotal);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Ignore(x => x.LineTotal);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class InvoiceConfiguration : AuditableEntityConfiguration<Invoice>
{
    public override void Configure(EntityTypeBuilder<Invoice> builder)
    {
        base.Configure(builder);
        builder.ToTable("Invoices");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.FiscalStatus).HasConversion<int>();
        builder.Property(x => x.FiscalReference).HasMaxLength(128);
        builder.Property(x => x.IssuedBy).HasMaxLength(64);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.CustomerId);
        builder.Ignore(x => x.SubTotal);
        builder.Ignore(x => x.TaxTotal);
        builder.Ignore(x => x.Total);
        builder.Ignore(x => x.Balance);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.TaxRate).HasPrecision(9, 4);
        builder.Ignore(x => x.LineSubTotal);
        builder.Ignore(x => x.LineTax);
        builder.Ignore(x => x.LineTotal);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public sealed class PaymentConfiguration : AuditableEntityConfiguration<Payment>
{
    public override void Configure(EntityTypeBuilder<Payment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Payments");

        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Method).HasConversion<int>();
        builder.Property(x => x.Reference).HasMaxLength(128);

        builder.HasIndex(x => x.InvoiceId);
    }
}
