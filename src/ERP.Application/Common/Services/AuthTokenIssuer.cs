using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;

namespace ERP.Application.Common.Services;

/// <inheritdoc />
internal sealed class AuthTokenIssuer : IAuthTokenIssuer
{
    private readonly ITokenService _tokens;
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public AuthTokenIssuer(ITokenService tokens, IApplicationDbContext db, IDateTime clock)
    {
        _tokens = tokens;
        _db = db;
        _clock = clock;
    }

    public async Task<AuthenticationResult> IssueAsync(
        UserIdentity user, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = _clock.UtcNow.Add(_tokens.RefreshTokenLifetime),
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthenticationResult(
            user.Id, user.Email, user.Roles, access.Value, access.ExpiresAt, refresh);
    }
}
