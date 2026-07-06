using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Catalog;

public sealed class UnitOfMeasureConfiguration : AuditableEntityConfiguration<UnitOfMeasure>
{
    public override void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        base.Configure(builder);
        builder.ToTable("UnitsOfMeasure");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
    }
}
