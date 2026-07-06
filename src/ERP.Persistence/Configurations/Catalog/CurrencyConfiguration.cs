using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Catalog;

public sealed class CurrencyConfiguration : AuditableEntityConfiguration<Currency>
{
    public override void Configure(EntityTypeBuilder<Currency> builder)
    {
        base.Configure(builder);
        builder.ToTable("Currencies");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(3);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Symbol).HasMaxLength(8);

        // At most one base currency. Filtered unique index (SQL Server) allows many non-base rows.
        builder.HasIndex(x => x.IsBaseCurrency)
            .IsUnique()
            .HasFilter("[IsBaseCurrency] = 1");
    }
}
