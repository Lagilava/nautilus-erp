namespace ERP.Domain.Auditing;

/// <summary>The kind of change recorded in the audit trail.</summary>
public enum AuditAction
{
    Created = 1,
    Modified = 2,
    Deleted = 3
}

/// <summary>
/// An immutable record of a change to a business entity, produced by the persistence-layer
/// audit interceptor. Deliberately NOT a <c>BaseEntity</c> — it is the audit sink and must
/// not itself be audited or soft-deleted.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>CLR type name of the changed entity (e.g. "Invoice").</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key of the changed entity, as a string.</summary>
    public string EntityId { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    /// <summary>JSON of the changed columns: for updates, old→new; for inserts, new values.</summary>
    public string? Changes { get; set; }

    /// <summary>User who made the change (null for system/background operations).</summary>
    public string? UserId { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
