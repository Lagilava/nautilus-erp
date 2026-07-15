using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Issuing a sales invoice must auto-post a balanced journal entry (Dr Accounts Receivable,
/// Cr Sales Revenue + Cr Sales Tax Payable), and that entry must feed the trial balance.
/// </summary>
public class AccountingEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AccountingEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync() => await _factory.AdminClientAsync();

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    [Fact]
    public async Task Issuing_an_invoice_posts_a_balanced_journal_entry()
    {
        var client = await AdminClientAsync();
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
        var customer = await IdAsync(await client.PostAsJsonAsync("/api/customers", new
        {
            code = $"CUST{s}", name = "Acme", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = "Fiji",
            taxIdentificationNumber = (string?)null, creditLimit = 0.0
        }));

        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId = product, warehouseId = warehouse, quantity = 100.0, unitCost = 4.0, reference = "GRN", notes = (string?)null }));

        var order = await IdAsync(await client.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = customer,
            warehouseId = warehouse,
            orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 10.0, unitPrice = 10.0 } },
            notes = (string?)null
        }));
        await client.PostAsync($"/api/sales-orders/{order}/confirm", null);
        await client.PostAsync($"/api/sales-orders/{order}/fulfill", null);

        // 10 x 10 = 100 net, 15% VAT = 15, total 115.
        var invoice = await IdAsync(await client.PostAsJsonAsync("/api/invoices/from-order",
            new { salesOrderId = order, issueDate = "2026-07-02", dueDate = "2026-07-30" }));
        await client.PostAsync($"/api/invoices/{invoice}/issue", null);

        // A posted, balanced journal entry with source SalesInvoice (2) should now exist.
        var entries = await client.GetFromJsonAsync<JsonElement>(
            "/api/journal-entries?source=2&pageSize=200", Json);
        var items = entries.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);

        var last = items[0]; // most recent first
        Assert.Equal("Posted", last.GetProperty("status").GetString());
        Assert.Equal(115m, last.GetProperty("totalDebits").GetDecimal());
        Assert.Equal(115m, last.GetProperty("totalCredits").GetDecimal());

        // The trial balance should reflect the posting: AR debited 115, revenue+tax credited 115.
        var trialBalance = await client.GetFromJsonAsync<JsonElement>(
            "/api/reports/trial-balance/data", Json);
        var rows = trialBalance.GetProperty("rows").EnumerateArray().ToList();
        var arRow = rows.FirstOrDefault(r => r[0].GetString() == "1100");
        Assert.True(arRow.ValueKind != JsonValueKind.Undefined, "Expected an Accounts Receivable row.");
        Assert.Equal("115.00", arRow[3].GetString()); // Debit column
    }
}
