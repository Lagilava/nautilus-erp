using ERP.Domain.Identity;

namespace ERP.UnitTests.Domain;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_is_true_for_unrevoked_unexpired_token()
    {
        var token = new RefreshToken { ExpiresAt = Now.AddDays(1) };
        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_is_false_when_expired()
    {
        var token = new RefreshToken { ExpiresAt = Now.AddSeconds(-1) };
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void IsActive_is_false_once_revoked()
    {
        var token = new RefreshToken { ExpiresAt = Now.AddDays(1) };
        token.Revoke(Now, replacedByTokenHash: "next-token-hash");

        Assert.False(token.IsActive(Now));
        Assert.Equal(Now, token.RevokedAt);
        Assert.Equal("next-token-hash", token.ReplacedByTokenHash);
    }
}
