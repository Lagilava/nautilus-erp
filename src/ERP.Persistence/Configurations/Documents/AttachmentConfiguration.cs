using ERP.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Documents;

public sealed class AttachmentConfiguration : AuditableEntityConfiguration<Attachment>
{
    public override void Configure(EntityTypeBuilder<Attachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Attachments");

        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(260);

        // The lookup path is always "attachments for this record" — index the pair.
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
