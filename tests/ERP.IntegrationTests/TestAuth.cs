using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Auth helpers for tests. There is no public self-registration any more, so non-admin users
/// are created the way the product creates them: by an administrator via POST /api/users.
/// </summary>
public static class TestAuth
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record AuthResponse(string AccessToken, string RefreshToken);

    public static async Task<HttpClient> AdminClientAsync(this ErpWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        await AuthenticateAsync(client, ErpWebApplicationFactory.AdminEmail, ErpWebApplicationFactory.AdminPassword);
        return client;
    }

    /// <summary>Creates a user with the given role (as admin) and returns a client signed in as them.</summary>
    public static async Task<HttpClient> ClientForNewUserAsync(
        this ErpWebApplicationFactory factory, string role, Guid? branchId = null)
    {
        var admin = await factory.AdminClientAsync();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@erp.local";
        const string password = "Str0ng#Pass1";

        var create = await admin.PostAsJsonAsync("/api/users", new
        {
            email, password, firstName = "Test", lastName = role, roles = new[] { role }, branchId,
        });
        create.EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        await AuthenticateAsync(client, email, password);
        return client;
    }

    public static async Task<AuthResponse> AuthenticateAsync(HttpClient client, string email, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }
}
