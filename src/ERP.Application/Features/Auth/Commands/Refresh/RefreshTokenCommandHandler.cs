using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using ERP.Shared.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Auth.Commands.Refresh;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthenticationResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public RefreshTokenCommandHandler(
        IApplicationDbContext db, IIdentityService identity, IAuthTokenIssuer tokenIssuer,
        ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResult>> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Tokens are stored hashed; look up by hash of the presented value.
        var presentedHash = TokenHasher.Hash(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

        // Unknown, expired, or already-used/revoked token — reject. Presenting a revoked
        // token is a reuse signal; we surface it as unauthorized rather than reissuing.
        if (existing is null || !existing.IsActive(now))
            return Result.Failure<AuthenticationResult>(
                ERP.Shared.Results.Error.Unauthorized("Invalid or expired refresh token."));

        var userResult = await _identity.GetByIdAsync(existing.UserId, cancellationToken);
        if (userResult.IsFailure)
            return Result.Failure<AuthenticationResult>(userResult.Error);

        // Issue the new pair first so we know the replacement token value, then rotate:
        // mark the old token revoked and link it to its successor for auditability.
        var auth = await _tokenIssuer.IssueAsync(userResult.Value, _currentUser.IpAddress, cancellationToken);

        existing.Revoke(now, TokenHasher.Hash(auth.RefreshToken));
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(auth);
    }
}
