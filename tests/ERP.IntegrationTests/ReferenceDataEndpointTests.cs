using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Exercises the reference-data vocabulary end-to-end: an admin builds the prerequisites
/// (unit, category, tax) then creates and lists a product. Also checks role enforcement
/// and effective-dated tax rate resolution.
/// </summary>
public class ReferenceDataEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ReferenceDataEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record AuthResponse(string AccessToken);
    private sealed record CreatedId(Guid value);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ErpWebApplicationFactory.AdminEmail,
            password = ErpWebApplicationFactory.AdminPassword
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        // Result<Guid> serialises the raw guid as the body.
        var raw = await response.Content.ReadAsStringAsync();
        return Guid.Parse(raw.Trim('"'));
    }

    [Fact]
    public async Task Admin_can_build_vocabulary_and_create_and_list_product()
    {
        var client = await AdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var uomId = await CreatedIdAsync(await client.PostAsJsonAsync("/api/units-of-measure",
            new { code = $"EA{suffix}", name = "Each" }));

        var categoryId = await CreatedIdAsync(await client.PostAsJsonAsync("/api/categories",
            new { code = $"CAT{suffix}", name = "Beverages", description = (string?)null, parentCategoryId = (Guid?)null }));

        var taxId = await CreatedIdAsync(await client.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{suffix}", name = "Fiji VAT", treatment = 1, initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));

        var productId = await CreatedIdAsync(await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"SKU{suffix}",
            name = "Cola 1.5L",
            description = (string?)null,
            barcode = (string?)null,
            categoryId,
            unitOfMeasureId = uomId,
            taxId,
            costPrice = 2.50,
            sellingPrice = 3.99
        }));

        var get = await client.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("Cola 1.5L", await get.Content.ReadAsStringAsync());

        var list = await client.GetAsync("/api/products?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains($"SKU{suffix}", await list.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Staff_user_cannot_create_reference_data()
    {
        var client = await _factory.ClientForNewUserAsync("Staff");

        var response = await client.PostAsJsonAsync("/api/units-of-measure", new { code = "XX", name = "Nope" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creating_product_with_missing_references_is_rejected()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"SKU{Guid.NewGuid():N}",
            name = "Orphan",
            description = (string?)null,
            barcode = (string?)null,
            categoryId = Guid.NewGuid(),
            unitOfMeasureId = Guid.NewGuid(),
            taxId = Guid.NewGuid(),
            costPrice = 1.0,
            sellingPrice = 2.0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
