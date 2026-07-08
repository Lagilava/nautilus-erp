using ERP.Domain.Common;

namespace ERP.Domain.Identity;

/// <summary>
/// A persisted refresh token. Access tokens are short-lived and stateless; refresh
/// tokens are long-lived, stored server-side, and rotated on every use so a stolen
/// token can be detected and revoked. Belongs to a user (by <see cref="UserId"/>);
/// the concrete user type lives in Persistence to keep the Domain framework-free.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the opaque token. The raw value is returned to the client once and
    /// never stored, so a database read cannot be replayed as a session.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is used (rotated) or explicitly revoked (logout).</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one on rotation — enables reuse detection.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
