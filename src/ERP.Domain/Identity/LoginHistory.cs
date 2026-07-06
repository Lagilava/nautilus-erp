using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// An audit record of an authentication attempt (success or failure). Recorded for
/// security monitoring — lockout forensics, impossible-travel, credential stuffing.
/// </summary>
public class LoginHistory : BaseEntity
{
    /// <summary>Null when the attempt used an unknown email (no user to attribute it to).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Email as supplied by the caller, so failed attempts on unknown accounts are still logged.</summary>
    public string AttemptedEmail { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    /// <summary>Reason on failure (e.g. "invalid_credentials", "locked_out"). Null on success.</summary>
    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
