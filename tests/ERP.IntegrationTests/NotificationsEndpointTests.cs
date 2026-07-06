using System.Net;

namespace ERP.IntegrationTests;

/// <summary>The SignalR notifications hub is authenticated-only.</summary>
public class NotificationsEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    public NotificationsEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Hub_negotiate_requires_authentication()
    {
        var client = _factory.CreateClient();

        // SignalR negotiate is a POST; the [Authorize] hub must reject anonymous callers.
        var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
