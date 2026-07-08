namespace ERP.Shared.Results;

/// <summary>
/// A machine-readable error with a stable code and a human-readable message.
/// Used by <see cref="Result"/> to convey failure without throwing.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Validation failure — the caller sent bad input.</summary>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Authentication/authorization failure.</summary>
    public static Error Unauthorized(string message) => new("unauthorized", message);

    /// <summary>Authenticated, but this action is not permitted (e.g. segregation of duties).</summary>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>A conflicting state (e.g. duplicate email).</summary>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Requested resource does not exist.</summary>
    public static Error NotFound(string message) => new("not_found", message);
}
