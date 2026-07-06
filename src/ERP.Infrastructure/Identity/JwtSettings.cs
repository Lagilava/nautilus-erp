namespace ERP.Infrastructure.Identity;

/// <summary>
/// JWT configuration bound from the "Jwt" configuration section. The signing key must
/// come from configuration/secrets — never hardcoded.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric signing key (HS256). Minimum 32 bytes; supply via secrets/env.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
