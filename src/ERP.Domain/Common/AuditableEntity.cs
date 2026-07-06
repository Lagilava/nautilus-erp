namespace ERP.Domain.Common;

/// <summary>
/// Marker base for reference/business entities. Currently identical to
/// <see cref="BaseEntity"/>, but gives module entities a distinct base so cross-cutting
/// behavior (e.g. the audit-logging interceptor in Milestone 7) can target them.
/// </summary>
public abstract class AuditableEntity : BaseEntity;
