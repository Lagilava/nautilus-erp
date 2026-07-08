using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using ERP.Shared.Results;
using ERP.Shared.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Auth.Commands.Refresh;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthenticationResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IApplicationDbContext db, IIdentityService identity, IAuthTokenIssuer tokenIssuer,
        ICurrentUserService currentUser, IDateTime clock, ILogger<RefreshTokenCommandHandler> logger)
    {
        _db = db;
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResult>> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Tokens are stored hashed; look up by hash of the presented value.
        var presentedHash = TokenHasher.Hash(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

        if (existing is null)
            return Result.Failure<AuthenticationResult>(
                ERP.Shared.Results.Error.Unauthorized("Invalid or expired refresh token."));

        // Presenting an already-rotated token proves the chain was duplicated: two parties hold
        // tokens from one login, and we cannot tell the thief from the victim. Revoke the whole
        // descendant chain so BOTH must re-authenticate. Rejecting only the presented token
        // would leave whoever redeemed it first — quite possibly the attacker — rotating freely.
        if (existing.RevokedAt is not null)
        {
            await RevokeDescendantsAsync(existing, now, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoked the token family.", existing.UserId);

            return Result.Failure<AuthenticationResult>(
                ERP.Shared.Results.Error.Unauthorized("Invalid or expired refresh token."));
        }

        if (!existing.IsActive(now))
            return Result.Failure<AuthenticationResult>(
                ERP.Shared.Results.Error.Unauthorized("Invalid or expired refresh token."));

        var userResult = await _identity.GetByIdAsync(existing.UserId, cancellationToken);
        if (userResult.IsFailure)
            return Result.Failure<AuthenticationResult>(userResult.Error);

        // Deactivating a user locks them out indefinitely, but lockout is only consulted on the
        // login path. Without this check a disabled account keeps rotating its refresh token —
        // and therefore keeps working — until the token's 7-day expiry.
        if (await _identity.IsLockedOutAsync(existing.UserId, cancellationToken))
            return Result.Failure<AuthenticationResult>(
                ERP.Shared.Results.Error.Unauthorized("Invalid or expired refresh token."));

        // Issue the new pair first so we know the replacement token value, then rotate:
        // mark the old token revoked and link it to its successor for auditability.
        var auth = await _tokenIssuer.IssueAsync(userResult.Value, _currentUser.IpAddress, cancellationToken);

        existing.Revoke(now, TokenHasher.Hash(auth.RefreshToken));
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(auth);
    }

    /// <summary>
    /// Walks <see cref="RefreshToken.ReplacedByTokenHash"/> forward from a reused token and
    /// revokes every successor, so the live token at the end of the chain dies with it.
    /// </summary>
    private async Task RevokeDescendantsAsync(RefreshToken reused, DateTimeOffset now, CancellationToken ct)
    {
        var family = await _db.RefreshTokens
            .Where(t => t.UserId == reused.UserId)
            .ToListAsync(ct);

        var byHash = family.ToDictionary(t => t.TokenHash);
        var cursor = reused;

        // The chain is finite (each token has at most one successor) but guard against a cycle
        // introduced by data corruption rather than looping forever.
        for (var hops = 0; hops < family.Count && cursor.ReplacedByTokenHash is { } next; hops++)
        {
            if (!byHash.TryGetValue(next, out var successor)) break;
            if (successor.RevokedAt is null) successor.Revoke(now);
            cursor = successor;
        }
    }
}
