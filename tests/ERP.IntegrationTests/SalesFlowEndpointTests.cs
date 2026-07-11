using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// The order-to-cash flow over the real API: build catalog + customer, stock a warehouse,
/// create → confirm → fulfil an order (which issues stock), raise an invoice with VAT from
/// the effective-dated tax engine, issue it (fiscalization stub → NotSubmitted), and take
/// partial then final payment to Paid.
/// </summary>
public class SalesFlowEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public SalesFlowEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync() => await _factory.AdminClientAsync();

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    [Fact]
    public async Task Order_to_cash_happy_path()
    {
        var client = await AdminClientAsync();
        var s = Guid.NewGuid().ToString("N")[..8];

        // Catalog + customer + warehouse.
        var uom = await IdAsync(await client.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await client.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await client.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "Fiji VAT", treatment = 1, initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));
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

        // Stock the warehouse so fulfilment can issue.
        await IdAsync(await client.PostAsJsonAsync("/api/inventory/receive",
            new { productId = product, warehouseId = warehouse, quantity = 100.0, unitCost = 4.0, reference = "GRN", notes = (string?)null }));

        // Create → confirm → fulfil order (10 units).
        var order = await IdAsync(await client.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = customer,
            warehouseId = warehouse,
            orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 10.0, unitPrice = 10.0 } },
            notes = (string?)null
        }));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/sales-orders/{order}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/sales-orders/{order}/fulfill", null)).StatusCode);

        // Stock dropped by 10 → 90 remain.
        var levels = await client.GetFromJsonAsync<JsonElement>($"/api/inventory/levels?warehouseId={warehouse}", Json);
        Assert.Equal(90, levels.GetProperty("items")[0].GetProperty("quantityOnHand").GetDecimal());

        // Invoice from order: 10 × 10 = 100 net, 15% VAT = 15, total 115.
        var invoice = await IdAsync(await client.PostAsJsonAsync("/api/invoices/from-order",
            new { salesOrderId = order, issueDate = "2026-07-02", dueDate = "2026-07-30" }));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/invoices/{invoice}/issue", null)).StatusCode);

        var issued = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{invoice}", Json);
        Assert.Equal(100m, issued.GetProperty("subTotal").GetDecimal());
        Assert.Equal(15m, issued.GetProperty("taxTotal").GetDecimal());
        Assert.Equal(115m, issued.GetProperty("total").GetDecimal());
        Assert.Equal("Issued", issued.GetProperty("status").GetString());
        Assert.Equal("NotSubmitted", issued.GetProperty("fiscalStatus").GetString()); // stub

        // Partial then final payment → Paid.
        await IdAsync(await client.PostAsJsonAsync($"/api/invoices/{invoice}/payments",
            new { invoiceId = invoice, amount = 50.0, paymentDate = "2026-07-05", method = 1, reference = (string?)null }));
        await IdAsync(await client.PostAsJsonAsync($"/api/invoices/{invoice}/payments",
            new { invoiceId = invoice, amount = 65.0, paymentDate = "2026-07-06", method = 1, reference = (string?)null }));

        var paid = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{invoice}", Json);
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
        Assert.Equal(0m, paid.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Fulfilling_without_stock_is_conflict_and_leaves_order_confirmed()
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
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 1.0, sellingPrice = 2.0
        }));
        var customer = await IdAsync(await client.PostAsJsonAsync("/api/customers", new
        {
            code = $"CUST{s}", name = "Acme", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null, creditLimit = 0.0
        }));

        var order = await IdAsync(await client.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = customer, warehouseId = warehouse, orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 5.0, unitPrice = 2.0 } }, notes = (string?)null
        }));
        await client.PostAsync($"/api/sales-orders/{order}/confirm", null);

        var fulfil = await client.PostAsync($"/api/sales-orders/{order}/fulfill", null);
        Assert.Equal(HttpStatusCode.Conflict, fulfil.StatusCode);

        var dto = await client.GetFromJsonAsync<JsonElement>($"/api/sales-orders/{order}", Json);
        Assert.Equal("Confirmed", dto.GetProperty("status").GetString()); // unchanged
    }
}
