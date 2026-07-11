using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Identity;

/// <summary>Issues HS256 JWT access tokens and cryptographically strong opaque refresh tokens.</summary>
public sealed class TokenService : ITokenService
{
    /// <summary>Custom claim carrying the user's branch scope (absent = unrestricted).</summary>
    public const string BranchClaim = "branch";

    // The MFA challenge token is a JWT with a distinct audience — never the API's own audience —
    // so that even if it were presented as a bearer token, JwtBearer's ValidateAudience check
    // (see DependencyInjection.AddJwtAuthentication) rejects it outright. It carries no role or
    // name-identifier claims, only the subject, so it grants no API access on its own; it can
    // only be redeemed via ValidateMfaChallengeToken.
    private const string MfaChallengeAudience = "ERP.MfaChallenge";
    private static readonly TimeSpan MfaChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly JwtSettings _settings;
    private readonly IDateTime _clock;

    public TokenService(IOptions<JwtSettings> settings, IDateTime clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_settings.RefreshTokenDays);

    public AccessToken CreateAccessToken(UserIdentity user)
    {
        var expiresAt = _clock.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Branch scope travels in the token so record-level filters need no extra lookup.
        if (user.BranchId is { } branchId)
            claims.Add(new Claim(BranchClaim, branchId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: _clock.UtcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string CreateMfaChallengeToken(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: MfaChallengeAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) },
            notBefore: _clock.UtcNow.UtcDateTime,
            expires: _clock.UtcNow.Add(MfaChallengeLifetime).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Result<Guid> ValidateMfaChallengeToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = MfaChallengeAudience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            // MapInboundClaims defaults to true, which renames short claim names like "sub" to
            // long legacy XML-namespace URIs (ClaimTypes.NameIdentifier). Disable it so the
            // "sub" claim this token was written with comes back out as "sub".
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out _);
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var userId)
                ? Result.Success(userId)
                : Result.Failure<Guid>(Error.Unauthorized("Invalid or expired MFA challenge."));
        }
        catch (SecurityTokenException)
        {
            return Result.Failure<Guid>(Error.Unauthorized("Invalid or expired MFA challenge."));
        }
        catch (ArgumentException)
        {
            // Malformed input (e.g. not a well-formed JWT string) throws ArgumentException
            // rather than a SecurityTokenException subtype.
            return Result.Failure<Guid>(Error.Unauthorized("Invalid or expired MFA challenge."));
        }
    }
}
