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

    /// <summary>Opaque high-entropy token value (hashed at rest is a later hardening step).</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is used (rotated) or explicitly revoked (logout).</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Token that replaced this one on rotation — enables reuse detection.</summary>
    public string? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now, string? replacedByToken = null)
    {
        RevokedAt = now;
        ReplacedByToken = replacedByToken;
    }
}
