using ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Organization;

public sealed class BranchConfiguration : AuditableEntityConfiguration<Branch>
{
    public override void Configure(EntityTypeBuilder<Branch> builder)
    {
        base.Configure(builder);
        builder.ToTable("Branches");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.AddressLine2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(32);

        builder.HasMany(x => x.Warehouses)
            .WithOne(w => w.Branch!)
            .HasForeignKey(w => w.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WarehouseConfiguration : AuditableEntityConfiguration<Warehouse>
{
    public override void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        base.Configure(builder);
        builder.ToTable("Warehouses");

        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
    }
}
