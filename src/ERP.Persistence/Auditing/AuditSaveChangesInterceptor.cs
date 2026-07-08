using System.Text.Json;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auditing;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Persistence.Auditing;

/// <summary>
/// Writes an <see cref="AuditLog"/> row for every insert/update/delete of a business
/// entity. Cross-cutting and implemented once here rather than per entity. Runs during
/// SaveChanges after audit-field stamping, so it captures final values. Because our Guid
/// keys are client-assigned, primary keys are already known at this point (no temp-value
/// second pass needed).
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser, IDateTime clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext context)
    {
        var now = _clock.UtcNow;
        var user = _currentUser.UserId?.ToString();

        // Snapshot first: adding AuditLog rows mutates the change tracker, so we must not
        // enumerate it while modifying it.
        //
        // RefreshToken and LoginHistory are excluded. Both are written on every login and every
        // token rotation, so auditing them buries real business changes in noise — and a
        // RefreshToken's serialized properties include its TokenHash, which would then outlive
        // the token itself in a table any Administrator can read.
        var audited = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not RefreshToken and not LoginHistory)
            .ToList();

        foreach (var entry in audited)
        {
            var (action, changes) = Describe(entry);
            context.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                Action = action,
                Changes = changes,
                UserId = user,
                Timestamp = now
            });
        }
    }

    private static (AuditAction action, string? changes) Describe(EntityEntry<BaseEntity> entry)
    {
        // A soft delete is modelled as an Update that sets DeletedAt; record it as Deleted.
        var isSoftDelete = entry.State == EntityState.Modified
            && entry.Property(nameof(BaseEntity.DeletedAt)).IsModified
            && entry.Entity.DeletedAt is not null;

        switch (entry.State)
        {
            case EntityState.Added:
                var added = entry.Properties
                    .Where(p => !IsNoise(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                return (AuditAction.Created, Serialize(added));

            case EntityState.Deleted:
                return (AuditAction.Deleted, null);

            default:
                var changed = entry.Properties
                    .Where(p => p.IsModified && !IsNoise(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => new { old = p.OriginalValue, @new = p.CurrentValue });
                return (isSoftDelete ? AuditAction.Deleted : AuditAction.Modified,
                        changed.Count == 0 ? null : Serialize(changed));
        }
    }

    // Exclude the audit/concurrency plumbing columns from recorded diffs.
    private static bool IsNoise(string name) => name is
        nameof(BaseEntity.RowVersion) or nameof(BaseEntity.CreatedAt) or nameof(BaseEntity.CreatedBy)
        or nameof(BaseEntity.UpdatedAt) or nameof(BaseEntity.ModifiedBy);

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
}
