using System.Net;
using System.Net.Http.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Maker-checker. Each rule breaks one link in the procure-to-pay fraud loop — raise an order,
/// approve it, "receive" the goods, approve the supplier's bill, pay it — so that no single
/// person can walk a transaction from creation to cash. The mirror rule on the sales side stops
/// the person who issued an invoice from voiding it after the money is collected.
///
/// Every test drives the real HTTP surface, because these rules are only worth anything if the
/// endpoints enforce them.
/// </summary>
public class SegregationOfDutiesEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;

    public SegregationOfDutiesEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    private sealed record Fixture(Guid WarehouseId, Guid ProductId, Guid SupplierId, Guid CustomerId);

    private static async Task<Fixture> SeedAsync(HttpClient admin)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        var uom = await IdAsync(await admin.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await admin.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await admin.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "VAT", treatment = "Standard", initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));
        var branch = await IdAsync(await admin.PostAsJsonAsync("/api/branches", new { code = $"B{s}", name = "Suva" }));
        var warehouse = await IdAsync(await admin.PostAsJsonAsync("/api/warehouses",
            new { code = $"W{s}", name = "Suva WH", branchId = branch }));
        var product = await IdAsync(await admin.PostAsJsonAsync("/api/products", new
        {
            sku = $"P{s}", name = "Widget", description = (string?)null, barcode = (string?)null,
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 5.0, sellingPrice = 10.0,
        }));
        var supplier = await IdAsync(await admin.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"SUP{s}", name = "Pacific Supplies", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null,
        }));
        var customer = await IdAsync(await admin.PostAsJsonAsync("/api/customers", new
        {
            code = $"CU{s}", name = "Acme", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null, creditLimit = 0.0,
        }));
        return new Fixture(warehouse, product, supplier, customer);
    }

    private static Task<HttpResponseMessage> CreatePoAsync(HttpClient client, Fixture f) =>
        client.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId = f.SupplierId, warehouseId = f.WarehouseId, orderDate = "2026-07-01",
            lines = new[] { new { productId = f.ProductId, quantity = 10.0, unitCost = 5.0 } },
            notes = (string?)null,
        });

    private static Task<HttpResponseMessage> ReceiveAsync(HttpClient client, Guid poId, Guid poLineId) =>
        client.PostAsJsonAsync($"/api/purchase-orders/{poId}/receipts", new
        {
            purchaseOrderId = poId, receivedDate = "2026-07-02",
            lines = new[] { new { purchaseOrderLineId = poLineId, quantity = 10.0 } },
            notes = (string?)null,
        });

    private static async Task<Guid> FirstLineIdAsync(HttpClient client, Guid poId)
    {
        var po = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/purchase-orders/{poId}");
        return po.GetProperty("lines").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task The_person_who_raised_a_purchase_order_cannot_approve_it()
    {
        var admin = await _factory.AdminClientAsync();
        var f = await SeedAsync(admin);
        var maker = await _factory.ClientForNewUserAsync("Manager");

        var poId = await IdAsync(await CreatePoAsync(maker, f));

        var self = await maker.PostAsync($"/api/purchase-orders/{poId}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
    }

    [Fact]
    public async Task A_second_manager_can_approve_a_purchase_order()
    {
        var admin = await _factory.AdminClientAsync();
        var f = await SeedAsync(admin);
        var maker = await _factory.ClientForNewUserAsync("Manager");
        var checker = await _factory.ClientForNewUserAsync("Manager");

        var poId = await IdAsync(await CreatePoAsync(maker, f));

        var approved = await checker.PostAsync($"/api/purchase-orders/{poId}/confirm", null);
        approved.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Goods_cannot_be_received_by_the_raiser_or_the_approver()
    {
        var admin = await _factory.AdminClientAsync();
        var f = await SeedAsync(admin);
        var maker = await _factory.ClientForNewUserAsync("Manager");
        var checker = await _factory.ClientForNewUserAsync("Manager");
        var storeman = await _factory.ClientForNewUserAsync("Manager");

        var poId = await IdAsync(await CreatePoAsync(maker, f));
        (await checker.PostAsync($"/api/purchase-orders/{poId}/confirm", null)).EnsureSuccessStatusCode();
        var lineId = await FirstLineIdAsync(admin, poId);

        Assert.Equal(HttpStatusCode.Forbidden, (await ReceiveAsync(maker, poId, lineId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReceiveAsync(checker, poId, lineId)).StatusCode);

        // A third pair of hands completes the receipt.
        (await ReceiveAsync(storeman, poId, lineId)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_supplier_invoice_cannot_be_approved_by_its_enterer_or_the_receiver_and_the_approver_cannot_pay_it()
    {
        var admin = await _factory.AdminClientAsync();
        var f = await SeedAsync(admin);
        var maker = await _factory.ClientForNewUserAsync("Manager");
        var checker = await _factory.ClientForNewUserAsync("Manager");
        var storeman = await _factory.ClientForNewUserAsync("Manager");
        var payables = await _factory.ClientForNewUserAsync("Manager");
        var treasury = await _factory.ClientForNewUserAsync("Manager");

        var poId = await IdAsync(await CreatePoAsync(maker, f));
        (await checker.PostAsync($"/api/purchase-orders/{poId}/confirm", null)).EnsureSuccessStatusCode();
        var lineId = await FirstLineIdAsync(admin, poId);
        (await ReceiveAsync(storeman, poId, lineId)).EnsureSuccessStatusCode();

        // Payables enters the bill…
        var invoiceId = await IdAsync(await payables.PostAsJsonAsync("/api/supplier-invoices/from-order", new
        {
            purchaseOrderId = poId, issueDate = "2026-07-03", dueDate = "2026-08-03", supplierReference = "INV-1",
        }));

        // …so payables may not approve it, and neither may the storeman who received the goods.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await payables.PostAsync($"/api/supplier-invoices/{invoiceId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await storeman.PostAsync($"/api/supplier-invoices/{invoiceId}/approve", null)).StatusCode);

        (await checker.PostAsync($"/api/supplier-invoices/{invoiceId}/approve", null)).EnsureSuccessStatusCode();

        // The approver must not also release the money.
        var payload = new
        {
            supplierInvoiceId = invoiceId, amount = 10.0, paymentDate = "2026-07-04",
            method = "BankTransfer", reference = (string?)null,
        };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await checker.PostAsJsonAsync($"/api/supplier-invoices/{invoiceId}/payments", payload)).StatusCode);

        (await treasury.PostAsJsonAsync($"/api/supplier-invoices/{invoiceId}/payments", payload)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_person_who_issued_an_invoice_cannot_void_it()
    {
        var admin = await _factory.AdminClientAsync();
        var f = await SeedAsync(admin);
        var issuer = await _factory.ClientForNewUserAsync("Manager");
        var supervisor = await _factory.ClientForNewUserAsync("Manager");

        // Stock, so the sales order can be fulfilled.
        await IdAsync(await admin.PostAsJsonAsync("/api/inventory/receive", new
        {
            productId = f.ProductId, warehouseId = f.WarehouseId, quantity = 10.0, unitCost = 5.0,
            reference = (string?)null, notes = (string?)null,
        }));

        var orderId = await IdAsync(await admin.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = f.CustomerId, warehouseId = f.WarehouseId, orderDate = "2026-07-01",
            lines = new[] { new { productId = f.ProductId, quantity = 2.0, unitPrice = 10.0 } }, notes = (string?)null,
        }));
        (await admin.PostAsync($"/api/sales-orders/{orderId}/confirm", null)).EnsureSuccessStatusCode();

        var invoiceId = await IdAsync(await admin.PostAsJsonAsync("/api/invoices/from-order", new
        {
            salesOrderId = orderId, issueDate = "2026-07-02", dueDate = "2026-08-02",
        }));

        (await issuer.PostAsync($"/api/invoices/{invoiceId}/issue", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await issuer.PostAsync($"/api/invoices/{invoiceId}/void", null)).StatusCode);
        (await supervisor.PostAsync($"/api/invoices/{invoiceId}/void", null)).EnsureSuccessStatusCode();
    }
}
