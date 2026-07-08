using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Record-level security: a user scoped to a branch sees and touches only that branch's
/// warehouse-bound data. An unscoped user (e.g. the administrator) sees everything.
/// </summary>
public class BranchScopeEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public BranchScopeEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private static async Task<Guid> IdAsync(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return Guid.Parse((await r.Content.ReadAsStringAsync()).Trim('"'));
    }

    /// <summary>Two branches, each with one warehouse holding stock of its own product.</summary>
    private async Task<(Guid branchA, Guid whA, Guid prodA, Guid branchB, Guid whB, Guid prodB)> SeedTwoBranchesAsync(
        HttpClient admin)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        var uom = await IdAsync(await admin.PostAsJsonAsync("/api/units-of-measure", new { code = $"EA{s}", name = "Each" }));
        var cat = await IdAsync(await admin.PostAsJsonAsync("/api/categories",
            new { code = $"C{s}", name = "Cat", description = (string?)null, parentCategoryId = (Guid?)null }));
        var tax = await IdAsync(await admin.PostAsJsonAsync("/api/taxes",
            new { code = $"VAT{s}", name = "VAT", treatment = "Standard", initialPercentage = 15.0, effectiveFrom = "2020-01-01" }));

        var branchA = await IdAsync(await admin.PostAsJsonAsync("/api/branches", new { code = $"BA{s}", name = "Suva" }));
        var branchB = await IdAsync(await admin.PostAsJsonAsync("/api/branches", new { code = $"BB{s}", name = "Lautoka" }));
        var whA = await IdAsync(await admin.PostAsJsonAsync("/api/warehouses", new { code = $"WA{s}", name = "Suva WH", branchId = branchA }));
        var whB = await IdAsync(await admin.PostAsJsonAsync("/api/warehouses", new { code = $"WB{s}", name = "Lautoka WH", branchId = branchB }));

        async Task<Guid> Product(string sku) => await IdAsync(await admin.PostAsJsonAsync("/api/products", new
        {
            sku, name = sku, description = (string?)null, barcode = (string?)null,
            categoryId = cat, unitOfMeasureId = uom, taxId = tax, costPrice = 5.0, sellingPrice = 10.0,
        }));
        var prodA = await Product($"PA{s}");
        var prodB = await Product($"PB{s}");

        foreach (var (p, w) in new[] { (prodA, whA), (prodB, whB) })
            await IdAsync(await admin.PostAsJsonAsync("/api/inventory/receive",
                new { productId = p, warehouseId = w, quantity = 10.0, unitCost = 5.0, reference = (string?)null, notes = (string?)null }));

        return (branchA, whA, prodA, branchB, whB, prodB);
    }

    [Fact]
    public async Task Branch_scoped_user_sees_only_their_branch_stock()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, prodA, _, _, prodB) = await SeedTwoBranchesAsync(admin);

        // Admin is unscoped and sees both products' stock.
        var all = await admin.GetFromJsonAsync<JsonElement>("/api/inventory/levels?pageSize=100", Json);
        var allIds = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        Assert.Contains(prodA, allIds);
        Assert.Contains(prodB, allIds);

        // A manager scoped to branch A sees only branch A's stock.
        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);
        var mine = await scoped.GetFromJsonAsync<JsonElement>("/api/inventory/levels?pageSize=100", Json);
        var mineIds = mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();

        Assert.Contains(prodA, mineIds);
        Assert.DoesNotContain(prodB, mineIds);
    }

    [Fact]
    public async Task Branch_scoped_user_cannot_receive_stock_into_another_branch()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, whB, prodB) = await SeedTwoBranchesAsync(admin);

        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);

        var response = await scoped.PostAsJsonAsync("/api/inventory/receive", new
        {
            productId = prodB, warehouseId = whB, quantity = 5.0, unitCost = 5.0,
            reference = (string?)null, notes = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Branch_scoped_user_cannot_create_a_sales_order_in_another_branch()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, whB, prodB) = await SeedTwoBranchesAsync(admin);
        var s = Guid.NewGuid().ToString("N")[..8];
        var customer = await IdAsync(await admin.PostAsJsonAsync("/api/customers", new
        {
            code = $"CU{s}", name = "Acme", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null, creditLimit = 0.0,
        }));

        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);

        var response = await scoped.PostAsJsonAsync("/api/sales-orders", new
        {
            customerId = customer, warehouseId = whB, orderDate = "2026-07-01",
            lines = new[] { new { productId = prodB, quantity = 1.0, unitPrice = 10.0 } }, notes = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A GUID is not an access token. Fetching a single record must be scoped exactly as listing
    /// them is, or a branch-scoped user reads another branch's cost prices by quoting an id they
    /// saw in an audit log or a shared URL.
    /// </summary>
    [Fact]
    public async Task Branch_scoped_user_cannot_read_another_branches_purchase_order()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, whB, prodB) = await SeedTwoBranchesAsync(admin);
        var s = Guid.NewGuid().ToString("N")[..8];
        var supplier = await IdAsync(await admin.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"SUP{s}", name = "Globex", email = (string?)null, phone = (string?)null,
            addressLine1 = (string?)null, city = (string?)null, country = (string?)null,
            taxIdentificationNumber = (string?)null,
        }));
        var po = await IdAsync(await admin.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId = supplier, warehouseId = whB, orderDate = "2026-07-01",
            lines = new[] { new { productId = prodB, quantity = 5.0, unitCost = 5.0 } }, notes = (string?)null,
        }));

        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);

        var response = await scoped.GetAsync($"/api/purchase-orders/{po}");

        // NotFound, not Forbidden — a 403 would confirm the order exists. The admin, unscoped,
        // still reads it, proving the 404 is authorization and not a broken fixture.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        (await admin.GetAsync($"/api/purchase-orders/{po}")).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Adjustment is the one command that creates or destroys stock with no counterparty
    /// document — the classic shrinkage-fraud vector. It must be branch-scoped.
    /// </summary>
    [Fact]
    public async Task Branch_scoped_user_cannot_adjust_stock_in_another_branch()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, whB, prodB) = await SeedTwoBranchesAsync(admin);

        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);

        var response = await scoped.PostAsJsonAsync("/api/inventory/adjust", new
        {
            productId = prodB, warehouseId = whB, quantityDelta = -5.0,
            unitCost = (decimal?)null, reason = "stock take",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Omitting warehouseId must not widen an export past the caller's branch, and Staff must not
    /// be able to export company-wide cost prices at all.
    /// </summary>
    [Fact]
    public async Task Inventory_valuation_export_is_scoped_and_denied_to_staff()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, _, _) = await SeedTwoBranchesAsync(admin);

        var staff = await _factory.ClientForNewUserAsync("Staff");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/reports/inventory-valuation")).StatusCode);

        // A scoped manager may export, but sees only their own branch's value.
        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);
        var scopedCsv = await scoped.GetStringAsync("/api/reports/inventory-valuation");
        var adminCsv = await admin.GetStringAsync("/api/reports/inventory-valuation");

        Assert.True(scopedCsv.Split('\n').Length < adminCsv.Split('\n').Length);
    }

    [Fact]
    public async Task Branch_scoped_dashboard_reports_only_that_branch()
    {
        var admin = await _factory.AdminClientAsync();
        var (branchA, _, _, _, _, _) = await SeedTwoBranchesAsync(admin);

        var adminDash = await admin.GetFromJsonAsync<JsonElement>("/api/dashboard", Json);
        var scoped = await _factory.ClientForNewUserAsync("Manager", branchA);
        var scopedDash = await scoped.GetFromJsonAsync<JsonElement>("/api/dashboard", Json);

        // Both branches hold 10 x $5 = $50; the scoped user must see strictly less than the whole group.
        Assert.True(scopedDash.GetProperty("inventoryValue").GetDecimal() < adminDash.GetProperty("inventoryValue").GetDecimal());
        Assert.True(scopedDash.GetProperty("inventoryValue").GetDecimal() > 0);
    }
}
