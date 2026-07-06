namespace ERP.Domain.Common;

/// <summary>
/// Base for all persisted entities. Enforces the project-wide conventions:
/// GUID PK, audit trail, soft delete, and an optimistic-concurrency token.
/// EF mapping (column types, the rowversion, the soft-delete query filter) is
/// configured in the Persistence layer — this type stays persistence-ignorant.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Audit trail.
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // Soft delete — rows are never physically removed; a global query filter hides them.
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;

    // Optimistic concurrency token (maps to SQL Server rowversion).
    public byte[]? RowVersion { get; set; }
}
