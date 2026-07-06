namespace ERP.Application.Common.Models;

/// <summary>
/// What the client receives after a successful login/refresh: a short-lived access
/// token plus a rotating refresh token. Returned by the auth command handlers.
/// </summary>
public sealed record AuthenticationResult(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken);
