using ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Organization;

public sealed class CompanyProfileConfiguration : AuditableEntityConfiguration<CompanyProfile>
{
    public override void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        base.Configure(builder);
        builder.ToTable("CompanyProfiles");

        builder.Property(x => x.LegalName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TradingName).HasMaxLength(200);
        builder.Property(x => x.Tin).HasMaxLength(32);
        builder.Property(x => x.AddressLine1).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.BaseCurrency).IsRequired().HasMaxLength(3);
    }
}
