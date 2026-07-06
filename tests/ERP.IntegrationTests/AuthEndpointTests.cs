using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// End-to-end auth flow over the real pipeline: register, login, access a protected
/// endpoint, rotate the refresh token, and log out. Covers the key failure paths too.
/// </summary>
public class AuthEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record AuthResponse(
        Guid UserId, string Email, string[] Roles,
        string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

    private static object NewUser(string email) => new
    {
        email,
        password = "Str0ng#Pass1",
        firstName = "Test",
        lastName = "User"
    };

    [Fact]
    public async Task Register_then_login_and_access_protected_endpoint()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";

        var register = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.Contains("Staff", auth.Roles);

        // Protected endpoint rejects anonymous, accepts the bearer token.
        var anon = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(email, await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong#Pass9" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_is_rejected()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Json);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotEqual(auth.RefreshToken, rotated!.RefreshToken);

        // The consumed (rotated) token must no longer be accepted — reuse detection.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_error()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "weak", firstName = "A", lastName = "B"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_conflict()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@erp.local";
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email));

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
