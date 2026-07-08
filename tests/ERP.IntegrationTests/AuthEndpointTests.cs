using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>
/// End-to-end auth over the real pipeline, including the security guarantees:
/// no public self-registration, reset tokens never returned to the caller, refresh-token
/// rotation with reuse detection, and self-service that cannot escalate privilege.
/// </summary>
public class AuthEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record AuthResponse(Guid UserId, string Email, string[] Roles, string AccessToken, string RefreshToken);

    [Fact]
    public async Task There_is_no_public_self_registration_endpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "intruder@evil.test", password = "Str0ng#Pass1", firstName = "In", lastName = "Truder",
        });

        // The route no longer exists — a stranger cannot mint themselves an account.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_created_user_can_sign_in_and_reach_a_protected_endpoint()
    {
        var client = await _factory.ClientForNewUserAsync("Staff");

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("Staff", await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_callers()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ErpWebApplicationFactory.AdminEmail, password = "Wrong#Pass9",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_never_returns_a_reset_token_and_does_not_reveal_account_existence()
    {
        var client = _factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = ErpWebApplicationFactory.AdminEmail });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = $"nobody-{Guid.NewGuid():N}@erp.local" });

        // Identical, empty responses: no token, no account-existence oracle.
        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
        Assert.Empty(await known.Content.ReadAsStringAsync());
        Assert.Empty(await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_rotates_token_and_the_consumed_token_is_rejected()
    {
        var client = _factory.CreateClient();
        var auth = await TestAuth.AuthenticateAsync(client, ErpWebApplicationFactory.AdminEmail, ErpWebApplicationFactory.AdminPassword);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotEqual(auth.RefreshToken, rotated!.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = _factory.CreateClient();
        var auth = await TestAuth.AuthenticateAsync(client, ErpWebApplicationFactory.AdminEmail, ErpWebApplicationFactory.AdminPassword);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_tokens_are_not_stored_in_plaintext()
    {
        var client = _factory.CreateClient();
        var auth = await TestAuth.AuthenticateAsync(client, ErpWebApplicationFactory.AdminEmail, ErpWebApplicationFactory.AdminPassword);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ERP.Persistence.ApplicationDbContext>();
        var stored = await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync();

        Assert.NotEmpty(stored);
        Assert.DoesNotContain(auth.RefreshToken, stored);
        Assert.Contains(ERP.Shared.Security.TokenHasher.Hash(auth.RefreshToken), stored);
    }

    // ---- Self-service ----

    [Fact]
    public async Task User_can_update_their_own_display_name()
    {
        var client = await _factory.ClientForNewUserAsync("Staff");

        var update = await client.PutAsJsonAsync("/api/auth/me", new { firstName = "Renamed", lastName = "Person" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", Json);
        Assert.Equal("Renamed", me.GetProperty("firstName").GetString());
        // Role is unchanged — self-service cannot escalate privilege.
        Assert.Contains("Staff", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Change_password_requires_the_current_password()
    {
        var client = await _factory.ClientForNewUserAsync("Staff");

        var wrong = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "NotMyPassword#1", newPassword = "Brand#NewPass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var right = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Str0ng#Pass1", newPassword = "Brand#NewPass1" });
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);
    }
}
