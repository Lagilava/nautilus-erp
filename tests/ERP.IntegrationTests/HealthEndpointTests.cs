using System.Net;

namespace ERP.IntegrationTests;

/// <summary>
/// Smoke tests for the API host wiring. Proves the app boots and the health
/// endpoint responds — the minimum "the scaffold actually runs" guarantee.
/// </summary>
public class HealthEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;

    public HealthEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_returns_ok_and_healthy_status()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
    }
}
