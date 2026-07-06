using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Inventory flow over the real API: build a product + two warehouses, receive two cost
/// layers, issue across them and check FIFO COGS in the ledger, transfer between
/// warehouses, and verify low-stock and insufficient-stock handling.
/// </summary>
public class InventoryEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public InventoryEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private sealed record AuthResponse(string AccessToken);

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

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<(Guid productId, Guid wh1, Guid wh2)> SeedCatalogAsync(HttpClient client)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        var uom = await IdAsync(await client.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await client.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await client.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "VAT", treatment = 1, initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));
        var wh1 = await IdAsync(await client.PostAsJsonAsync("/api/branches", new { code = $"B{s}", name = "Main" }));
        var branchId = wh1;
        var whA = await IdAsync(await client.PostAsJsonAsync("/api/warehouses", new { code = $"WA{s}", name = "WH-A", branchId }));
        var whB = await IdAsync(await client.PostAsJsonAsync("/api/warehouses", new { code = $"WB{s}", name = "WH-B", branchId }));
        var product = await IdAsync(await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"SKU{s}", name = "Widget", description = (string?)null, barcode = (string?)null,
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 0.0, sellingPrice = 0.0
        }));
        return (product, whA, whB);
    }

    [Fact]
    public async Task Receive_then_issue_records_fifo_cogs_and_levels()
    {
        var client = await AdminClientAsync();
        var (productId, wh, _) = await SeedCatalogAsync(client);

        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId, warehouseId = wh, quantity = 10.0, unitCost = 2.0, reference = "GRN1", notes = (string?)null }));
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId, warehouseId = wh, quantity = 10.0, unitCost = 3.0, reference = "GRN2", notes = (string?)null }));

        // Issue 15 → 10@2 + 5@3 = 35 COGS; 5 remain @3 → value 15.
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/issue",
            new { productId, warehouseId = wh, quantity = 15.0, reference = "SO1", notes = (string?)null }));

        var levels = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventory/levels?warehouseId={wh}", Json);
        var row = levels.GetProperty("items")[0];
        Assert.Equal(5, row.GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(15m, row.GetProperty("stockValue").GetDecimal());

        var movements = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventory/movements?productId={productId}", Json);
        var issue = movements.GetProperty("items").EnumerateArray()
            .First(m => m.GetProperty("type").GetString() == "Issue");
        Assert.Equal(35m, issue.GetProperty("totalCost").GetDecimal());
    }

    [Fact]
    public async Task Issue_more_than_available_is_conflict()
    {
        var client = await AdminClientAsync();
        var (productId, wh, _) = await SeedCatalogAsync(client);
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId, warehouseId = wh, quantity = 3.0, unitCost = 1.0, reference = (string?)null, notes = (string?)null }));

        var response = await client.PostAsJsonAsync("/api/inventory/issue",
            new { productId, warehouseId = wh, quantity = 5.0, reference = (string?)null, notes = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_preserves_quantity_across_warehouses()
    {
        var client = await AdminClientAsync();
        var (productId, whA, whB) = await SeedCatalogAsync(client);
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId, warehouseId = whA, quantity = 10.0, unitCost = 4.0, reference = (string?)null, notes = (string?)null }));

        var transfer = await client.PostAsJsonAsync("/api/inventory/transfer", new
        {
            productId, fromWarehouseId = whA, toWarehouseId = whB, quantity = 6.0,
            reference = "TR1", notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.NoContent, transfer.StatusCode);

        var bLevels = await client.GetFromJsonAsync<JsonElement>($"/api/inventory/levels?warehouseId={whB}", Json);
        Assert.Equal(6, bLevels.GetProperty("items")[0].GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(24m, bLevels.GetProperty("items")[0].GetProperty("stockValue").GetDecimal()); // 6 × 4.0
    }

    [Fact]
    public async Task Low_stock_filter_returns_items_at_or_below_reorder_level()
    {
        var client = await AdminClientAsync();
        var (productId, wh, _) = await SeedCatalogAsync(client);
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId, warehouseId = wh, quantity = 5.0, unitCost = 1.0, reference = (string?)null, notes = (string?)null }));
        var setReorder = await client.PutAsJsonAsync("/api/inventory/reorder-level",
            new { productId, warehouseId = wh, reorderLevel = 10.0 });
        Assert.Equal(HttpStatusCode.NoContent, setReorder.StatusCode);

        var low = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventory/levels?warehouseId={wh}&lowStockOnly=true", Json);
        Assert.Equal(1, low.GetProperty("totalCount").GetInt32());
        Assert.True(low.GetProperty("items")[0].GetProperty("isBelowReorder").GetBoolean());
    }
}
