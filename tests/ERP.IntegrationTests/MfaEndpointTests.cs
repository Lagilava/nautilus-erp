using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// End-to-end MFA: enrolling an authenticator, the two-step login it then requires,
/// recovery-code redemption, and disabling it again. Codes are computed with a hand-rolled
/// RFC 6238 TOTP generator against the real shared secret the API returns, so this exercises
/// the exact algorithm ASP.NET Identity's authenticator provider uses — no test-only bypass.
/// </summary>
public class MfaEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public MfaEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record LoginResponse(bool MfaRequired, string? MfaChallengeToken, JsonElement? Tokens);

    /// <summary>Enrolls a fresh Staff user's authenticator and turns MFA on. Returns the shared secret and recovery codes.</summary>
    private async Task<(TestAuth.NewUser User, string SharedKey, string[] RecoveryCodes)> EnrollAsync()
    {
        var user = await _factory.NewUserAsync("Staff");

        var setup = await user.Client.PostAsync("/api/auth/mfa/setup", null);
        setup.EnsureSuccessStatusCode();
        var setupBody = await setup.Content.ReadFromJsonAsync<JsonElement>(Json);
        var sharedKey = setupBody.GetProperty("sharedKey").GetString()!;

        var code = Totp.ComputeCode(sharedKey);
        var enable = await user.Client.PostAsJsonAsync("/api/auth/mfa/enable", new { code });
        enable.EnsureSuccessStatusCode();
        var recoveryCodes = (await enable.Content.ReadFromJsonAsync<JsonElement>(Json))
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        return (user, sharedKey, recoveryCodes);
    }

    [Fact]
    public async Task Enabling_mfa_makes_subsequent_logins_require_a_challenge()
    {
        var (user, sharedKey, _) = await EnrollAsync();

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);

        Assert.True(body!.MfaRequired);
        Assert.NotNull(body.MfaChallengeToken);
        Assert.Null(body.Tokens);
    }

    [Fact]
    public async Task Redeeming_the_challenge_with_a_valid_code_issues_tokens()
    {
        var (user, sharedKey, _) = await EnrollAsync();

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        var challenge = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json))!.MfaChallengeToken;

        var verify = await _factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken = challenge, code = Totp.ComputeCode(sharedKey) });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = await verify.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Redeeming_the_challenge_with_a_wrong_code_is_unauthorized()
    {
        var (user, _, _) = await EnrollAsync();

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        var challenge = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json))!.MfaChallengeToken;

        var verify = await _factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken = challenge, code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_can_redeem_the_challenge_exactly_once()
    {
        var (user, _, recoveryCodes) = await EnrollAsync();
        var recoveryCode = recoveryCodes[0];

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        var challenge = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json))!.MfaChallengeToken;

        var first = await _factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken = challenge, code = recoveryCode });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A second login, same recovery code — must already be consumed.
        var login2 = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        var challenge2 = (await login2.Content.ReadFromJsonAsync<LoginResponse>(Json))!.MfaChallengeToken;

        var second = await _factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken = challenge2, code = recoveryCode });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task An_expired_or_forged_challenge_token_is_rejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { challengeToken = "not-a-real-token", code = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disabling_mfa_requires_the_current_password_and_restores_single_step_login()
    {
        var (user, _, _) = await EnrollAsync();

        var wrongPassword = await user.Client.PostAsJsonAsync("/api/auth/mfa/disable", new { currentPassword = "Wrong#Pass9" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var disable = await user.Client.PostAsJsonAsync("/api/auth/mfa/disable", new { currentPassword = user.Password });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = user.Password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.False(body!.MfaRequired);
        Assert.NotNull(body.Tokens);
    }

    [Fact]
    public async Task Get_current_user_reflects_mfa_enabled_state()
    {
        var (user, _, _) = await EnrollAsync();
        var me = await user.Client.GetFromJsonAsync<JsonElement>("/api/auth/me", Json);
        Assert.True(me.GetProperty("mfaEnabled").GetBoolean());
    }

    /// <summary>Minimal RFC 6238 TOTP (HMAC-SHA1, 30s step, 6 digits) matching ASP.NET Identity's authenticator provider.</summary>
    private static class Totp
    {
        public static string ComputeCode(string base32Secret)
        {
            var key = Base32Decode(base32Secret);
            var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            var hash = new HMACSHA1(key).ComputeHash(counterBytes);
            var offset = hash[^1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24)
                         | ((hash[offset + 1] & 0xFF) << 16)
                         | ((hash[offset + 2] & 0xFF) << 8)
                         | (hash[offset + 3] & 0xFF);

            return (binary % 1_000_000).ToString("D6");
        }

        private static byte[] Base32Decode(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            input = input.TrimEnd('=').ToUpperInvariant();

            var bits = 0;
            var value = 0;
            var output = new List<byte>();

            foreach (var c in input)
            {
                value = (value << 5) | alphabet.IndexOf(c);
                bits += 5;
                if (bits >= 8)
                {
                    output.Add((byte)((value >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }
            return output.ToArray();
        }
    }
}
