using ERP.Application.Common.Models;
using ERP.Shared.Results;

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

    /// <summary>
    /// A short-lived, signed token naming the user who passed the password step but still
    /// owes a second factor. Carries no authority beyond redemption via
    /// <see cref="ValidateMfaChallengeToken"/> — it grants no API access on its own.
    /// </summary>
    string CreateMfaChallengeToken(Guid userId);

    /// <summary>Validates a challenge token's signature, expiry, and purpose; returns the user id it names.</summary>
    Result<Guid> ValidateMfaChallengeToken(string token);
}
