using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations;

/// <summary>
/// Base configuration applying the conventions every business entity shares: primary key,
/// rowversion concurrency token, and the soft-delete global query filter. Derived configs
/// call <c>base.Configure</c> then add their own mapping.
/// </summary>
public abstract class AuditableEntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : AuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
