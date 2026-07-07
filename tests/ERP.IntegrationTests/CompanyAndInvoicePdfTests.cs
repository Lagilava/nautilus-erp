using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>Company profile, sales-trend, and the Fiji tax-invoice PDF.</summary>
public class CompanyAndInvoicePdfTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CompanyAndInvoicePdfTests(ErpWebApplicationFactory factory) => _factory = factory;

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

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    [Fact]
    public async Task Company_profile_can_be_updated_and_read_back()
    {
        var client = await AdminClientAsync();

        var update = await client.PutAsJsonAsync("/api/company", new
        {
            legalName = "Nautilus Trading (Fiji) Ltd",
            tradingName = "Nautilus",
            tin = "12-34567-8-9",
            addressLine1 = "1 Victoria Parade",
            city = "Suva",
            country = "Fiji",
            phone = "+679 330 0000",
            email = "hello@nautilus.fj",
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/company", Json);
        Assert.Equal("Nautilus Trading (Fiji) Ltd", profile.GetProperty("legalName").GetString());
        Assert.Equal("12-34567-8-9", profile.GetProperty("tin").GetString());
        Assert.Equal("FJD", profile.GetProperty("baseCurrency").GetString());
    }

    [Fact]
    public async Task Sales_trend_returns_a_continuous_six_month_series()
    {
        var client = await AdminClientAsync();
        var trend = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/sales-trend", Json);
        Assert.Equal(6, trend.GetArrayLength());
        foreach (var point in trend.EnumerateArray())
        {
            Assert.False(string.IsNullOrEmpty(point.GetProperty("label").GetString()));
            Assert.True(point.GetProperty("total").GetDecimal() >= 0);
        }
    }

    [Fact]
    public async Task Issued_invoice_downloads_as_a_pdf_tax_invoice()
    {
        var client = await AdminClientAsync();
        var s = Guid.NewGuid().ToString("N")[..8];

        var uom = await IdAsync(await client.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await client.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await client.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "VAT", treatment = "Standard", initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));
        var branch = await IdAsync(await client.PostAsJsonAsync("/api/branches", new { code = $"B{s}", name = "Main" }));
        var wh = await IdAsync(await client.PostAsJsonAsync("/api/warehouses", new { code = $"W{s}", name = "WH", branchId = branch }));
        var product = await IdAsync(await client.PostAsJsonAsync("/api/products", new
        {
            sku = $"SKU{s}", name = "Widget", description = (string?)null, barcode = (string?)null,
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 4.0, sellingPrice = 10.0,
        }));
        var customer = await IdAsync(await client.PostAsJsonAsync("/api/customers", new
        {
            code = $"CUST{s}", name = "Acme", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = "Suva", country = "Fiji",
            taxIdentificationNumber = "98-76543-2-1", creditLimit = 0.0,
        }));
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId = product, warehouseId = wh, quantity = 20.0, unitCost = 4.0, reference = (string?)null, notes = (string?)null }));

        var so = await IdAsync(await client.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = customer, warehouseId = wh, orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 5.0, unitPrice = 10.0 } }, notes = (string?)null,
        }));
        await client.PostAsync($"/api/sales-orders/{so}/confirm", null);
        var invoice = await IdAsync(await client.PostAsJsonAsync("/api/invoices/from-order",
            new { salesOrderId = so, issueDate = "2026-07-02", dueDate = (string?)null }));
        await client.PostAsync($"/api/invoices/{invoice}/issue", null);

        var pdf = await client.GetAsync($"/api/invoices/{invoice}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
