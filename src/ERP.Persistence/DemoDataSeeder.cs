using ERP.Domain.Catalog;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Domain.Taxation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Persistence;

/// <summary>
/// Seeds comprehensive demo data for demonstration purposes.
/// Run manually via Program.cs or a temporary endpoint.
/// </summary>
public static class DemoDataSeeder
{
    private static readonly Guid MainBranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MainWarehouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FjdCurrencyId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UsdCurrencyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid VatTaxId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SvtTaxId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid EachUomId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid CartonUomId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid LitreUomId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static async Task SeedAsync(IServiceProvider services, ILogger? logger = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is ready
        await db.Database.EnsureCreatedAsync();

        // Skip if already seeded
        if (await db.Customers.AnyAsync(c => c.Code == "CUST-001"))
        {
            logger?.LogInformation("Demo data already exists. Skipping.");
            return;
        }

        logger?.LogInformation("Seeding demo data...");

        // Reference data
        await SeedCurrenciesAsync(db);
        await SeedUnitsOfMeasureAsync(db);
        await SeedCategoriesAsync(db);
        await SeedTaxesAsync(db);
        await SeedOrganizationAsync(db);

        // Catalog
        await SeedProductsAsync(db);

        // Partners
        await SeedCustomersAsync(db);
        await SeedSuppliersAsync(db);

        // Inventory
        await SeedInventoryAsync(db);

        // Sales transactions
        await SeedSalesOrdersAsync(db);
        await SeedInvoicesAsync(db);
        await SeedPurchaseOrdersAsync(db);
        await SeedGoodsReceiptsAsync(db);
        await SeedSupplierInvoicesAsync(db);

        await db.SaveChangesAsync();
        logger?.LogInformation("Demo data seeding complete.");
    }

    private static async Task SeedCurrenciesAsync(ApplicationDbContext db)
    {
        db.Currencies.AddRange(
            new Currency { Id = FjdCurrencyId, Code = "FJD", Name = "Fiji Dollar", Symbol = "F$", IsBaseCurrency = true, IsActive = true },
            new Currency { Id = UsdCurrencyId, Code = "USD", Name = "US Dollar", Symbol = "$", IsBaseCurrency = false, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedUnitsOfMeasureAsync(ApplicationDbContext db)
    {
        db.UnitsOfMeasure.AddRange(
            new UnitOfMeasure { Id = EachUomId, Code = "EA", Name = "Each", IsActive = true },
            new UnitOfMeasure { Id = CartonUomId, Code = "CTN", Name = "Carton", IsActive = true },
            new UnitOfMeasure { Id = LitreUomId, Code = "L", Name = "Litre", IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext db)
    {
        db.Categories.AddRange(
            new Category { Code = "CAT-001", Name = "Electronics", IsActive = true },
            new Category { Code = "CAT-002", Name = "Groceries", IsActive = true },
            new Category { Code = "CAT-003", Name = "Beverages", IsActive = true }
        );
        await db.SaveChangesAsync();
        var defaultCategory = await db.Categories.FirstAsync(c => c.Code == "CAT-001");
    }

    private static async Task SeedTaxesAsync(ApplicationDbContext db)
    {
        var vat = new Tax { Id = VatTaxId, Code = "VAT", Name = "Value Added Tax", Treatment = TaxTreatment.Standard, IsActive = true };
        var svt = new Tax { Id = SvtTaxId, Code = "SVT", Name = "Service Tax", Treatment = TaxTreatment.Standard, IsActive = true };

        db.Taxes.AddRange(vat, svt);
        await db.SaveChangesAsync();

        db.TaxRates.AddRange(
            new TaxRate { TaxId = VatTaxId, Percentage = 0.15m, EffectiveFrom = new DateOnly(2024, 1, 1) },
            new TaxRate { TaxId = SvtTaxId, Percentage = 0.05m, EffectiveFrom = new DateOnly(2024, 1, 1) }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedOrganizationAsync(ApplicationDbContext db)
    {
        var mainBranch = new Branch
        {
            Id = MainBranchId,
            Code = "BR-001",
            Name = "Suva Main Branch",
            AddressLine1 = "123 Victoria Parade",
            City = "Suva",
            Country = "Fiji",
            Phone = "+679 331 2345",
            Email = "suva@nautilus-erp.com.fj",
            IsActive = true
        };

        db.Branches.Add(mainBranch);
        await db.SaveChangesAsync();

        db.Warehouses.Add(
            new Warehouse
            {
                Id = MainWarehouseId,
                Code = "WH-001",
                Name = "Central Warehouse",
                BranchId = MainBranchId,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        var electronics = await db.Categories.FirstAsync(c => c.Code == "CAT-001");
        var groceries = await db.Categories.FirstAsync(c => c.Code == "CAT-002");
        var beverages = await db.Categories.FirstAsync(c => c.Code == "CAT-003");
        var vat = VatTaxId;
        var each = EachUomId;
        var carton = CartonUomId;
        var litre = LitreUomId;

        db.Products.AddRange(
            new Product { Sku = "ELEC-001", Name = "Wireless Headphones", Description = "Bluetooth 5.0 noise-cancelling", CategoryId = electronics.Id, UnitOfMeasureId = each, TaxId = vat, CostPrice = 45.00m, SellingPrice = 89.99m, IsActive = true },
            new Product { Sku = "ELEC-002", Name = "USB-C Charging Cable", CategoryId = electronics.Id, UnitOfMeasureId = each, TaxId = vat, CostPrice = 3.50m, SellingPrice = 9.99m, IsActive = true },
            new Product { Sku = "ELEC-003", Name = "Power Bank 20000mAh", CategoryId = electronics.Id, UnitOfMeasureId = each, TaxId = vat, CostPrice = 22.00m, SellingPrice = 49.99m, IsActive = true },
            new Product { Sku = "GROC-001", Name = "Basmati Rice 5kg", CategoryId = groceries.Id, UnitOfMeasureId = carton, TaxId = vat, CostPrice = 12.50m, SellingPrice = 19.99m, IsActive = true },
            new Product { Sku = "GROC-002", Name = "Canned Tuna", CategoryId = groceries.Id, UnitOfMeasureId = each, TaxId = vat, CostPrice = 1.20m, SellingPrice = 2.50m, IsActive = true },
            new Product { Sku = "GROC-003", Name = "Cooking Oil 2L", CategoryId = groceries.Id, UnitOfMeasureId = litre, TaxId = vat, CostPrice = 4.80m, SellingPrice = 7.99m, IsActive = true },
            new Product { Sku = "BEV-001", Name = "Fiji Water 1.5L (case)", CategoryId = beverages.Id, UnitOfMeasureId = carton, TaxId = vat, CostPrice = 8.00m, SellingPrice = 14.99m, IsActive = true },
            new Product { Sku = "BEV-002", Name = "Coca-Cola 2L", CategoryId = beverages.Id, UnitOfMeasureId = litre, TaxId = vat, CostPrice = 1.80m, SellingPrice = 3.49m, IsActive = true },
            new Product { Sku = "BEV-003", Name = "Green Tea 500ml", CategoryId = beverages.Id, UnitOfMeasureId = each, TaxId = vat, CostPrice = 1.50m, SellingPrice = 3.99m, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedCustomersAsync(ApplicationDbContext db)
    {
        db.Customers.AddRange(
            new Customer { Code = "CUST-001", Name = "ABC Trading Ltd", Email = "orders@abctrading.com.fj", Phone = "+679 321 4567", AddressLine1 = "45 Renwick Road", City = "Suva", Country = "Fiji", TaxIdentificationNumber = "TIN-001234", CreditLimit = 15000m, IsActive = true },
            new Customer { Code = "CUST-002", Name = "Island Fresh Supermarket", Email = "buyer@islandfresh.com.fj", Phone = "+679 334 5678", AddressLine1 = "78 Namadi Heights", City = "Suva", Country = "Fiji", TaxIdentificationNumber = "TIN-005678", CreditLimit = 25000m, IsActive = true },
            new Customer { Code = "CUST-003", Name = "Sunset Beach Resort", Email = "procurement@sunsetresort.com.fj", Phone = "+679 666 1234", AddressLine1 = "Coral Coast", City = "Sigatoka", Country = "Fiji", CreditLimit = 10000m, IsActive = true },
            new Customer { Code = "CUST-004", Name = "Fiji Maritime Services", Email = "supply@fijimaritime.com.fj", Phone = "+679 323 4567", AddressLine1 = "12 Kings Wharf", City = "Suva", Country = "Fiji", CreditLimit = 5000m, IsActive = true },
            new Customer { Code = "CUST-005", Name = "Vanua Fresh Produce", Email = "sales@vanuafresh.com.fj", Phone = "+679 998 7654", City = "Nadi", Country = "Fiji", CreditLimit = 8000m, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedSuppliersAsync(ApplicationDbContext db)
    {
        db.Suppliers.AddRange(
            new Supplier { Code = "SUPP-001", Name = "Pacific Distributors Pty Ltd", Email = "sales@pacificdist.com.au", Phone = "+61 2 9876 5432", AddressLine1 = "1 Export Drive", City = "Brisbane", Country = "Australia", IsActive = true },
            new Supplier { Code = "SUPP-002", Name = "Fiji Beverages Ltd", Email = "orders@fijibrew.com.fj", Phone = "+679 992 3456", City = "Lautoka", Country = "Fiji", IsActive = true },
            new Supplier { Code = "SUPP-003", Name = "Global Electronics Supplies", Email = "info@globalelec.cn", Phone = "+86 755 1234 5678", IsActive = true },
            new Supplier { Code = "SUPP-004", Name = "Prime Agriculture Co-op", Email = "export@primeagri.co.nz", Phone = "+64 9 555 1234", IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedInventoryAsync(ApplicationDbContext db)
    {
        var warehouse = MainWarehouseId;
        var products = await db.Products.ToListAsync();

        foreach (var product in products)
        {
            decimal initialQty;

            if (product.Sku.StartsWith("ELEC"))
            {
                initialQty = product.Sku switch
                {
                    "ELEC-001" => 45m,
                    "ELEC-002" => 200m,
                    "ELEC-003" => 30m,
                    _ => 20m
                };
            }
            else if (product.Sku.StartsWith("BEV"))
            {
                initialQty = product.Sku switch
                {
                    "BEV-001" => 100m,
                    "BEV-002" => 250m,
                    "BEV-003" => 180m,
                    _ => 50m
                };
            }
            else
            {
                initialQty = product.Sku switch
                {
                    "GROC-001" => 60m,
                    "GROC-002" => 300m,
                    "GROC-003" => 80m,
                    _ => 40m
                };
            }

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                WarehouseId = warehouse,
                ReorderLevel = 10
            };

            item.Receive(initialQty, product.CostPrice);
            db.InventoryItems.Add(item);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSalesOrdersAsync(ApplicationDbContext db)
    {
        var customers = await db.Customers.ToListAsync();
        var products = await db.Products.ToListAsync();
        var warehouse = MainWarehouseId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var so1 = new SalesOrder
        {
            Number = "SO-2024-001",
            CustomerId = customers.First(c => c.Code == "CUST-001").Id,
            WarehouseId = warehouse,
            OrderDate = today.AddDays(-10),
            Notes = "Priority delivery"
        };
        so1.AddLine(products.First(p => p.Sku == "ELEC-001").Id, 5, 89.99m);
        so1.AddLine(products.First(p => p.Sku == "ELEC-002").Id, 10, 9.99m);
        so1.Confirm();

        var so2 = new SalesOrder
        {
            Number = "SO-2024-002",
            CustomerId = customers.First(c => c.Code == "CUST-002").Id,
            WarehouseId = warehouse,
            OrderDate = today.AddDays(-7),
            Notes = ""
        };
        so2.AddLine(products.First(p => p.Sku == "GROC-001").Id, 12, 19.99m);
        so2.AddLine(products.First(p => p.Sku == "BEV-001").Id, 6, 14.99m);
        so2.AddLine(products.First(p => p.Sku == "GROC-002").Id, 24, 2.50m);
        so2.Confirm();
        so2.MarkFulfilled();

        var so3 = new SalesOrder
        {
            Number = "SO-2024-003",
            CustomerId = customers.First(c => c.Code == "CUST-003").Id,
            WarehouseId = warehouse,
            OrderDate = today,
            Notes = "Follow up on room allocation"
        };
        so3.AddLine(products.First(p => p.Sku == "ELEC-003").Id, 2, 49.99m);

        var so4 = new SalesOrder
        {
            Number = "SO-2024-004",
            CustomerId = customers.First(c => c.Code == "CUST-005").Id,
            WarehouseId = warehouse,
            OrderDate = today.AddDays(-14),
            Notes = "Customer postponed order"
        };
        so4.AddLine(products.First(p => p.Sku == "GROC-003").Id, 10, 7.99m);
        so4.Cancel();

        db.SalesOrders.AddRange(so1, so2, so3, so4);
        await db.SaveChangesAsync();
    }

    private static async Task SeedInvoicesAsync(ApplicationDbContext db)
    {
        var customers = await db.Customers.ToListAsync();
        var products = await db.Products.ToListAsync();
        var vatRate = 0.15m;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var inv1 = new Invoice
        {
            Number = "INV-2024-001",
            CustomerId = customers.First(c => c.Code == "CUST-001").Id,
            IssueDate = today.AddDays(-8),
            DueDate = today.AddDays(22)
        };
        inv1.AddLine(products.First(p => p.Sku == "ELEC-001").Id, "Wireless Headphones", 5, 89.99m, vatRate);
        inv1.Issue();
        inv1.SetFiscalResult(FiscalStatus.Submitted, "FMS-REF-001123");

        var inv2 = new Invoice
        {
            Number = "INV-2024-002",
            CustomerId = customers.First(c => c.Code == "CUST-002").Id,
            IssueDate = today.AddDays(-5),
            DueDate = today.AddDays(25)
        };
        inv2.AddLine(products.First(p => p.Sku == "GROC-001").Id, "Basmati Rice 5kg", 12, 19.99m, vatRate);
        inv2.AddLine(products.First(p => p.Sku == "BEV-001").Id, "Fiji Water case", 6, 14.99m, vatRate);
        inv2.AddLine(products.First(p => p.Sku == "GROC-002").Id, "Canned Tuna", 24, 2.50m, vatRate);
        inv2.Issue();
        inv2.SetFiscalResult(FiscalStatus.Submitted, "FMS-REF-001124");
        inv2.ApplyPayment(448.29m);

        var inv3 = new Invoice
        {
            Number = "INV-2024-003",
            CustomerId = customers.First(c => c.Code == "CUST-003").Id,
            IssueDate = today.AddDays(-2),
            DueDate = today.AddDays(28)
        };
        inv3.AddLine(products.First(p => p.Sku == "ELEC-003").Id, "Power Bank", 1, 49.99m, vatRate);
        inv3.AddLine(products.First(p => p.Sku == "ELEC-002").Id, "USB-C Cable", 2, 9.99m, vatRate);
        inv3.Issue();
        inv3.SetFiscalResult(FiscalStatus.Submitted, "FMS-REF-001125");
        inv3.ApplyPayment(114.43m);

        var inv4 = new Invoice
        {
            Number = "INV-2024-004",
            CustomerId = customers.First(c => c.Code == "CUST-004").Id,
            IssueDate = today.AddDays(-20),
            DueDate = today.AddDays(10)
        };
        inv4.AddLine(products.First(p => p.Sku == "BEV-002").Id, "Coca-Cola 2L", 10, 3.49m, vatRate);

        db.Invoices.AddRange(inv1, inv2, inv3, inv4);
        await db.SaveChangesAsync();
    }

    private static async Task SeedPurchaseOrdersAsync(ApplicationDbContext db)
    {
        var suppliers = await db.Suppliers.ToListAsync();
        var products = await db.Products.ToListAsync();

        var po1 = new PurchaseOrder
        {
            Number = "PO-2024-001",
            SupplierId = suppliers.First(s => s.Code == "SUPP-001").Id,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-12)
        };
        po1.AddLine(products.First(p => p.Sku == "ELEC-001").Id, 50, 45.00m);
        po1.AddLine(products.First(p => p.Sku == "ELEC-003").Id, 40, 22.00m);
        po1.Confirm();
        po1.ReceiveLine(po1.Lines.First().Id, po1.Lines.First().Quantity);
        po1.ReceiveLine(po1.Lines.Last().Id, po1.Lines.Last().Quantity);

        var po2 = new PurchaseOrder
        {
            Number = "PO-2024-002",
            SupplierId = suppliers.First(s => s.Code == "SUPP-002").Id,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3)
        };
        po2.AddLine(products.First(p => p.Sku == "BEV-001").Id, 200, 8.00m);
        po2.Confirm();

        var po3 = new PurchaseOrder
        {
            Number = "PO-2024-003",
            SupplierId = suppliers.First(s => s.Code == "SUPP-003").Id,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)
        };
        po3.AddLine(products.First(p => p.Sku == "ELEC-002").Id, 500, 3.50m);

        db.PurchaseOrders.AddRange(po1, po2, po3);
        await db.SaveChangesAsync();
    }

    private static async Task SeedGoodsReceiptsAsync(ApplicationDbContext db)
    {
        var po = await db.PurchaseOrders.FirstAsync(p => p.Number == "PO-2024-001");
        var products = await db.Products.ToListAsync();
        var warehouse = MainWarehouseId;

        var receipt = new GoodsReceipt
        {
            PurchaseOrderId = po.Id,
            WarehouseId = warehouse,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10),
            Notes = "Received in good condition",
            Lines =
            {
                new GoodsReceiptLine { PurchaseOrderLineId = po.Lines.First(l => l.ProductId == products.First(p => p.Sku == "ELEC-001").Id).Id, ProductId = products.First(p => p.Sku == "ELEC-001").Id, Quantity = 50, UnitCost = 45.00m },
                new GoodsReceiptLine { PurchaseOrderLineId = po.Lines.First(l => l.ProductId == products.First(p => p.Sku == "ELEC-003").Id).Id, ProductId = products.First(p => p.Sku == "ELEC-003").Id, Quantity = 40, UnitCost = 22.00m }
            }
        };

        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSupplierInvoicesAsync(ApplicationDbContext db)
    {
        var supplier = await db.Suppliers.FirstAsync(s => s.Code == "SUPP-001");
        var po = await db.PurchaseOrders.FirstAsync(p => p.Number == "PO-2024-001");
        var products = await db.Products.ToListAsync();

        var supInv = new SupplierInvoice
        {
            Number = "SINV-2024-001",
            SupplierId = supplier.Id,
            PurchaseOrderId = po.Id,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-9),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(21),
            SupplierReference = "SUPP-REF-001"
        };
        supInv.AddLine(products.First(p => p.Sku == "ELEC-001").Id, "Wireless Headphones", 50, 45.00m, 0.00m);
        supInv.AddLine(products.First(p => p.Sku == "ELEC-003").Id, "Power Bank", 40, 22.00m, 0.00m);
        supInv.Approve();
        supInv.ApplyPayment(2780.00m);

        db.SupplierInvoices.Add(supInv);
        await db.SaveChangesAsync();
    }
}