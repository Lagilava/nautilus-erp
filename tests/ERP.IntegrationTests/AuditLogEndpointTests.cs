using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>Verifies the cross-cutting audit interceptor records changes and that the trail is admin-only.</summary>
public class AuditLogEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuditLogEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync() => await _factory.AdminClientAsync();

    [Fact]
    public async Task Creating_an_entity_writes_a_created_audit_row()
    {
        var client = await AdminClientAsync();
        var code = $"CUST{Guid.NewGuid():N}"[..12];

        var create = await client.PostAsJsonAsync("/api/customers", new
        {
            code, name = "Audited Co", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null, creditLimit = 0.0
        });
        var customerId = Guid.Parse((await create.Content.ReadAsStringAsync()).Trim('"'));

        var logs = await client.GetFromJsonAsync<JsonElement>(
            $"/api/audit-logs?entityName=Customer&entityId={customerId}", Json);

        Assert.True(logs.GetProperty("totalCount").GetInt32() >= 1);
        var row = logs.GetProperty("items")[0];
        Assert.Equal("Created", row.GetProperty("action").GetString());
        Assert.Contains("Audited Co", row.GetProperty("changes").GetString());
    }

    [Fact]
    public async Task Audit_trail_is_forbidden_for_non_admins()
    {
        var client = await _factory.ClientForNewUserAsync("Manager");

        var response = await client.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
