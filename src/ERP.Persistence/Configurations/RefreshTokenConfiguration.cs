using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token).IsRequired().HasMaxLength(512);
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.ReplacedByToken).HasMaxLength(512);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
