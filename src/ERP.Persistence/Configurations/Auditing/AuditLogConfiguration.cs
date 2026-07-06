using ERP.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Auditing;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);

        // Key is client-assigned; keep EF from treating it as store-generated.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EntityId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Action).HasConversion<int>();
        builder.Property(x => x.UserId).HasMaxLength(64);

        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => x.Timestamp);
    }
}
