using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>Administrator-only user management: list, create with roles, and deactivate.</summary>
public class UserAdminEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public UserAdminEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record AuthResponse(string AccessToken);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ErpWebApplicationFactory.AdminEmail,
            password = ErpWebApplicationFactory.AdminPassword,
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Admin_can_create_a_manager_and_they_can_sign_in()
    {
        var client = await AdminClientAsync();
        var email = $"manager-{Guid.NewGuid():N}@erp.local";

        var create = await client.PostAsJsonAsync("/api/users", new
        {
            email, password = "Manager#123", firstName = "Mana", lastName = "Ger", roles = new[] { "Manager" },
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        // The new manager can authenticate and is listed with the Manager role.
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "Manager#123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var users = await client.GetFromJsonAsync<JsonElement>("/api/users", Json);
        var created = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == email);
        Assert.Contains("Manager", created.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Deactivated_user_cannot_sign_in()
    {
        var client = await AdminClientAsync();
        var email = $"staff-{Guid.NewGuid():N}@erp.local";
        var create = await client.PostAsJsonAsync("/api/users", new
        {
            email, password = "Staff#1234", firstName = "St", lastName = "Aff", roles = new[] { "Staff" },
        });
        var userId = Guid.Parse((await create.Content.ReadAsStringAsync()).Trim('"'));

        var deactivate = await client.PostAsJsonAsync($"/api/users/{userId}/active", false);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "Staff#1234" });
        Assert.NotEqual(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task User_admin_is_forbidden_for_non_admins()
    {
        var client = _factory.CreateClient();
        var email = $"staff-{Guid.NewGuid():N}@erp.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "Str0ng#Pass1", firstName = "S", lastName = "T",
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }
}
