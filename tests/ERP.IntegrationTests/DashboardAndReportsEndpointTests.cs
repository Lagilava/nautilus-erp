using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>Dashboard KPIs and multi-format report export.</summary>
public class DashboardAndReportsEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DashboardAndReportsEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync() => await _factory.AdminClientAsync();

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<(Guid product, Guid warehouse)> SeedStockAsync(HttpClient client)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        var uom = await IdAsync(await client.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await client.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await client.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "VAT", treatment = 1, initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));
        var branch = await IdAsync(await client.PostAsJsonAsync("/api/branches", new { code = $"B{s}", name = "Main" }));
        var warehouse = await IdAsync(await client.PostAsJsonAsync("/api/warehouses", new { code = $"W{s}", name = "WH", branchId = branch }));
        var product = await IdAsync(await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"SKU{s}", name = "Widget", description = (string?)null, barcode = (string?)null,
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 4.0, sellingPrice = 10.0
        }));
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId = product, warehouseId = warehouse, quantity = 10.0, unitCost = 4.0, reference = (string?)null, notes = (string?)null }));
        return (product, warehouse);
    }

    [Fact]
    public async Task Dashboard_reports_inventory_value_and_counts()
    {
        var client = await AdminClientAsync();
        await SeedStockAsync(client);

        var dash = await client.GetFromJsonAsync<JsonElement>("/api/dashboard", Json);

        Assert.True(dash.GetProperty("productCount").GetInt32() >= 1);
        Assert.True(dash.GetProperty("inventoryValue").GetDecimal() >= 40m); // at least our 10 × 4.00
    }

    [Theory]
    [InlineData("csv", "text/csv")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("pdf", "application/pdf")]
    public async Task Inventory_valuation_exports_in_each_format(string ext, string expectedContentType)
    {
        var client = await AdminClientAsync();
        await SeedStockAsync(client);

        // ReportFormat: Csv=1, Excel=2, Pdf=3
        var format = ext switch { "csv" => 1, "xlsx" => 2, _ => 3 };
        var response = await client.GetAsync($"/api/reports/inventory-valuation?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);

        if (ext == "csv")
            Assert.Contains("SKU", System.Text.Encoding.UTF8.GetString(bytes));
        if (ext == "pdf")
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
