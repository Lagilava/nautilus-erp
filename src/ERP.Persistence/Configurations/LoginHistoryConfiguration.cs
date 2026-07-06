using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations;

public sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("LoginHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AttemptedEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasMaxLength(128);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.OccurredAt);

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
