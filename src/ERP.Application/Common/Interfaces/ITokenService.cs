using ERP.Application.Common.Models;

namespace ERP.Application.Common.Interfaces;

/// <summary>A freshly minted JWT access token and its absolute expiry.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues JWT access tokens and opaque refresh tokens. Implemented in Infrastructure
/// (holds the signing key). Refresh-token persistence/rotation is the handler's job.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(UserIdentity user);

    /// <summary>High-entropy opaque value stored server-side and returned to the client.</summary>
    string GenerateRefreshToken();

    /// <summary>Configured refresh-token lifetime, so handlers can stamp expiry consistently.</summary>
    TimeSpan RefreshTokenLifetime { get; }
}
