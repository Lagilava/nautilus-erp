using ERP.Domain.Taxation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Taxation;

public sealed class TaxConfiguration : AuditableEntityConfiguration<Tax>
{
    public override void Configure(EntityTypeBuilder<Tax> builder)
    {
        base.Configure(builder);
        builder.ToTable("Taxes");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Treatment).HasConversion<int>();

        builder.HasMany(x => x.Rates)
            .WithOne(r => r.Tax!)
            .HasForeignKey(r => r.TaxId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaxRateConfiguration : AuditableEntityConfiguration<TaxRate>
{
    public override void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        base.Configure(builder);
        builder.ToTable("TaxRates");

        builder.Property(x => x.Percentage).HasPrecision(9, 4);
        builder.HasIndex(x => new { x.TaxId, x.EffectiveFrom });
    }
}
