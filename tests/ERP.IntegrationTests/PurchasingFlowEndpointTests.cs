using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// The procure-to-pay flow: create supplier + catalog, raise and confirm a purchase order,
/// receive goods (which increases FIFO stock and advances the PO), then bill and pay the
/// supplier invoice through to Paid.
///
/// Each step is performed by a different person, because segregation of duties forbids one
/// actor from walking the chain alone (see <see cref="SegregationOfDutiesEndpointTests"/>).
/// </summary>
public class PurchasingFlowEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public PurchasingFlowEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

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

    [Fact]
    public async Task Procure_to_pay_happy_path()
    {
        var client = await AdminClientAsync();
        var buyer = await _factory.ClientForNewUserAsync("Manager");
        var approver = await _factory.ClientForNewUserAsync("Manager");
        var storeman = await _factory.ClientForNewUserAsync("Staff");
        var payables = await _factory.ClientForNewUserAsync("Manager");
        var treasury = await _factory.ClientForNewUserAsync("Manager");
        var s = Guid.NewGuid().ToString("N")[..8];

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
        var supplier = await IdAsync(await client.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"SUP{s}", name = "Globex", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = "Fiji", taxIdentificationNumber = (string?)null
        }));

        // PO for 20 @ 4.00 raised by the buyer, confirmed by a second manager.
        var po = await IdAsync(await buyer.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId = supplier, warehouseId = warehouse, orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 20.0, unitCost = 4.0 } }, notes = (string?)null
        }));
        Assert.Equal(HttpStatusCode.NoContent, (await approver.PostAsync($"/api/purchase-orders/{po}/confirm", null)).StatusCode);

        var poDto = await client.GetFromJsonAsync<JsonElement>($"/api/purchase-orders/{po}", Json);
        var poLineId = poDto.GetProperty("lines")[0].GetProperty("id").GetGuid();

        // Receive 8 → PartiallyReceived, stock 8.
        await IdAsync(await storeman.PostAsJsonAsync($"/api/purchase-orders/{po}/receipts", new
        {
            purchaseOrderId = po, receivedDate = "2026-07-03",
            lines = new[] { new { purchaseOrderLineId = poLineId, quantity = 8.0 } }, notes = (string?)null
        }));
        var afterPartial = await client.GetFromJsonAsync<JsonElement>($"/api/purchase-orders/{po}", Json);
        Assert.Equal("PartiallyReceived", afterPartial.GetProperty("status").GetString());

        // Receive remaining 12 → Received, stock 20.
        await IdAsync(await storeman.PostAsJsonAsync($"/api/purchase-orders/{po}/receipts", new
        {
            purchaseOrderId = po, receivedDate = "2026-07-05",
            lines = new[] { new { purchaseOrderLineId = poLineId, quantity = 12.0 } }, notes = (string?)null
        }));
        var afterFull = await client.GetFromJsonAsync<JsonElement>($"/api/purchase-orders/{po}", Json);
        Assert.Equal("Received", afterFull.GetProperty("status").GetString());

        var levels = await client.GetFromJsonAsync<JsonElement>($"/api/inventory/levels?warehouseId={warehouse}", Json);
        Assert.Equal(20, levels.GetProperty("items")[0].GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(80m, levels.GetProperty("items")[0].GetProperty("stockValue").GetDecimal()); // 20 × 4.00

        // Supplier invoice from PO: 20 × 4 = 80 net, 15% = 12, total 92.
        var sinv = await IdAsync(await payables.PostAsJsonAsync("/api/supplier-invoices/from-order",
            new { purchaseOrderId = po, issueDate = "2026-07-06", dueDate = "2026-07-31", supplierReference = "INV-EXT-1" }));
        Assert.Equal(HttpStatusCode.NoContent, (await approver.PostAsync($"/api/supplier-invoices/{sinv}/approve", null)).StatusCode);

        var approved = await client.GetFromJsonAsync<JsonElement>($"/api/supplier-invoices/{sinv}", Json);
        Assert.Equal(92m, approved.GetProperty("total").GetDecimal());
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        // Pay in full → Paid. Treasury releases the money, never the approver.
        await IdAsync(await treasury.PostAsJsonAsync($"/api/supplier-invoices/{sinv}/payments",
            new { supplierInvoiceId = sinv, amount = 92.0, paymentDate = "2026-07-10", method = 3, reference = "TT-1" }));
        var paid = await client.GetFromJsonAsync<JsonElement>($"/api/supplier-invoices/{sinv}", Json);
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
        Assert.Equal(0m, paid.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Over_receiving_a_line_is_conflict()
    {
        var client = await AdminClientAsync();
        var buyer = await _factory.ClientForNewUserAsync("Manager");
        var storeman = await _factory.ClientForNewUserAsync("Staff");
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
        var supplier = await IdAsync(await client.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"SUP{s}", name = "Globex", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null, taxIdentificationNumber = (string?)null
        }));
        var po = await IdAsync(await buyer.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId = supplier, warehouseId = warehouse, orderDate = "2026-07-01",
            lines = new[] { new { productId = product, quantity = 5.0, unitCost = 1.0 } }, notes = (string?)null
        }));
        (await client.PostAsync($"/api/purchase-orders/{po}/confirm", null)).EnsureSuccessStatusCode();
        var poDto = await client.GetFromJsonAsync<JsonElement>($"/api/purchase-orders/{po}", Json);
        var lineId = poDto.GetProperty("lines")[0].GetProperty("id").GetGuid();

        var over = await storeman.PostAsJsonAsync($"/api/purchase-orders/{po}/receipts", new
        {
            purchaseOrderId = po, receivedDate = "2026-07-03",
            lines = new[] { new { purchaseOrderLineId = lineId, quantity = 6.0 } }, notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
    }
}
