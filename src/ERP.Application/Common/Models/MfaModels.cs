namespace ERP.Application.Common.Models;

/// <summary>
/// A freshly (re)generated authenticator secret, not yet active. The caller must confirm
/// it with a valid code before MFA is actually enabled — see <c>EnableMfaCommand</c>.
/// </summary>
public sealed record MfaSetup(string SharedKey, string AuthenticatorUri);

/// <summary>
/// What the client receives from login: either a completed sign-in, or a short-lived
/// challenge that must be redeemed with a TOTP/recovery code via <c>VerifyMfaCommand</c>.
/// </summary>
public sealed record LoginResult(bool MfaRequired, string? MfaChallengeToken, AuthenticationResult? Tokens);
