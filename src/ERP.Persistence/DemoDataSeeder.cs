using ERP.Domain.Catalog;
using ERP.Domain.Inventory;
using ERP.Domain.Organization;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Domain.Taxation;
using ERP.Persistence.Identity;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Persistence;

/// <summary>
/// Seeds a coherent demo dataset so the app looks "alive" for a walkthrough.
///
/// The invariants this seeder must respect (they are enforced by the domain, so violating
/// them throws at startup):
///  • <see cref="TaxRate.Percentage"/> is a PERCENTAGE (15 = 15%), not a fraction.
///  • Payments may never exceed an invoice's outstanding balance, and every applied payment
///    must have a matching <see cref="Payment"/>/<see cref="SupplierPayment"/> record.
///  • Every change to stock on hand must be paired with a <see cref="StockMovement"/>, so the
///    ledger explains the levels.
///  • Fiscalization is NEVER faked: invoices stay <see cref="FiscalStatus.NotSubmitted"/>
///    because no verified FRCS/VMS adapter is configured.
/// </summary>
public static class DemoDataSeeder
{
    private static readonly Guid MainBranchId = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid MainWarehouseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FjdCurrencyId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UsdCurrencyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid VatTaxId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SttTaxId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid EachUomId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid CartonUomId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid LitreUomId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>Fiji VAT, as a percentage. Matches <c>InvoiceLine.LineTax = subtotal * rate / 100</c>.</summary>
    private const decimal VatPercent = 15m;

    // Opening stock per SKU, and the reorder level used to flag replenishment.
    private static readonly (string Sku, decimal Qty, decimal ReorderLevel)[] OpeningStock =
    [
        ("ELEC-001", 45m, 20m),
        ("ELEC-002", 200m, 50m),
        ("ELEC-003", 30m, 15m),
        ("GROC-001", 60m, 20m),
        ("GROC-002", 300m, 100m),
        ("GROC-003", 80m, 25m),
        ("BEV-001", 100m, 30m),
        ("BEV-002", 250m, 60m),
        ("BEV-003", 180m, 200m), // deliberately below reorder level, so "Needs attention" is non-empty
    ];

    public static async Task SeedAsync(IServiceProvider services, ILogger? logger = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Runs before the early-exit below: without a second and third person the
        // segregation-of-duties rules cannot be demonstrated at all.
        await SeedDemoUsersAsync(scope.ServiceProvider, logger);

        // Schema creation/migration is the initialiser's job — a seeder must not do it.
        if (await db.Customers.AnyAsync(c => c.Code == "CUST-001"))
        {
            logger?.LogInformation("Demo data already exists. Skipping.");
            return;
        }

        logger?.LogInformation("Seeding demo data...");

        await SeedCompanyProfileAsync(db);
        await SeedCurrenciesAsync(db);
        await SeedUnitsOfMeasureAsync(db);
        await SeedCategoriesAsync(db);
        await SeedTaxesAsync(db);
        await SeedOrganizationAsync(db);
        await SeedProductsAsync(db);
        await SeedCustomersAsync(db);
        await SeedSuppliersAsync(db);

        // Stock and the transactions that move it, in chronological order so the ledger reads true.
        var stock = await SeedOpeningStockAsync(db);
        await SeedPurchasingAsync(db, stock);
        await SeedSalesOrdersAsync(db, stock);
        await SeedInvoicesAndPaymentsAsync(db);
        await SeedSupplierInvoiceAsync(db);

        logger?.LogInformation("Demo data seeding complete.");
    }

    /// <summary>
    /// A manager and a storeman alongside the bootstrap administrator. Segregation of duties is
    /// only meaningful with more than one pair of hands: the manager raises a purchase order,
    /// the administrator approves it, the storeman receives it. All three are left unscoped to a
    /// branch because the demo company has exactly one.
    /// </summary>
    private static async Task SeedDemoUsersAsync(IServiceProvider services, ILogger? logger)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        (string Email, string First, string Last, string Role)[] demo =
        [
            ("manager@erp.local", "Mere", "Vakalala", Roles.Manager),
            ("staff@erp.local", "Josua", "Tuisawau", Roles.Staff),
        ];

        foreach (var (email, first, last, role) in demo)
        {
            if (await users.FindByEmailAsync(email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = first,
                LastName = last
            };

            // Same password policy as the bootstrap admin; these accounts only exist in demo data.
            var created = await users.CreateAsync(user, "Demo#12345");
            if (created.Succeeded)
            {
                await users.AddToRoleAsync(user, role);
                logger?.LogInformation("Seeded demo {Role} {Email}", role, email);
            }
            else
            {
                logger?.LogWarning("Failed to seed demo user {Email}: {Errors}",
                    email, string.Join(", ", created.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedCompanyProfileAsync(ApplicationDbContext db)
    {
        // The initialiser creates a placeholder row; give the demo a real Fiji identity so the
        // tax-invoice PDF carries a seller name and TIN.
        var company = await db.CompanyProfiles.FirstOrDefaultAsync();
        if (company is null) return;

        company.LegalName = "Nautilus Trading (Fiji) Pte Ltd";
        company.TradingName = "Nautilus";
        company.Tin = "50-12345-0-1";
        company.AddressLine1 = "123 Victoria Parade";
        company.City = "Suva";
        company.Country = "Fiji";
        company.Phone = "+679 331 2345";
        company.Email = "accounts@nautilus.com.fj";
        await db.SaveChangesAsync();
    }

    private static async Task SeedCurrenciesAsync(ApplicationDbContext db)
    {
        db.Currencies.AddRange(
            new Currency { Id = FjdCurrencyId, Code = "FJD", Name = "Fiji Dollar", Symbol = "F$", IsBaseCurrency = true },
            new Currency { Id = UsdCurrencyId, Code = "USD", Name = "US Dollar", Symbol = "$" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUnitsOfMeasureAsync(ApplicationDbContext db)
    {
        db.UnitsOfMeasure.AddRange(
            new UnitOfMeasure { Id = EachUomId, Code = "EA", Name = "Each" },
            new UnitOfMeasure { Id = CartonUomId, Code = "CTN", Name = "Carton" },
            new UnitOfMeasure { Id = LitreUomId, Code = "L", Name = "Litre" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext db)
    {
        db.Categories.AddRange(
            new Category { Code = "CAT-001", Name = "Electronics" },
            new Category { Code = "CAT-002", Name = "Groceries" },
            new Category { Code = "CAT-003", Name = "Beverages" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTaxesAsync(ApplicationDbContext db)
    {
        db.Taxes.AddRange(
            new Tax { Id = VatTaxId, Code = "VAT", Name = "Value Added Tax", Treatment = TaxTreatment.Standard },
            new Tax { Id = SttTaxId, Code = "STT", Name = "Service Turnover Tax", Treatment = TaxTreatment.Standard });
        await db.SaveChangesAsync();

        // Percentages, not fractions: 15 means 15%.
        db.TaxRates.AddRange(
            new TaxRate { TaxId = VatTaxId, Percentage = VatPercent, EffectiveFrom = new DateOnly(2024, 1, 1) },
            new TaxRate { TaxId = SttTaxId, Percentage = 5m, EffectiveFrom = new DateOnly(2024, 1, 1) });
        await db.SaveChangesAsync();
    }

    private static async Task SeedOrganizationAsync(ApplicationDbContext db)
    {
        db.Branches.Add(new Branch
        {
            Id = MainBranchId,
            Code = "BR-001",
            Name = "Suva Main Branch",
            AddressLine1 = "123 Victoria Parade",
            City = "Suva",
            Country = "Fiji",
            Phone = "+679 331 2345",
            Email = "suva@nautilus.com.fj",
            TaxIdentificationNumber = "50-12345-0-1",
        });
        await db.SaveChangesAsync();

        db.Warehouses.Add(new Warehouse
        {
            Id = MainWarehouseId,
            Code = "WH-001",
            Name = "Central Warehouse",
            BranchId = MainBranchId,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        var electronics = await db.Categories.FirstAsync(c => c.Code == "CAT-001");
        var groceries = await db.Categories.FirstAsync(c => c.Code == "CAT-002");
        var beverages = await db.Categories.FirstAsync(c => c.Code == "CAT-003");

        db.Products.AddRange(
            new Product { Sku = "ELEC-001", Name = "Wireless Headphones", Description = "Bluetooth 5.0 noise-cancelling", CategoryId = electronics.Id, UnitOfMeasureId = EachUomId, TaxId = VatTaxId, CostPrice = 45.00m, SellingPrice = 89.99m },
            new Product { Sku = "ELEC-002", Name = "USB-C Charging Cable", CategoryId = electronics.Id, UnitOfMeasureId = EachUomId, TaxId = VatTaxId, CostPrice = 3.50m, SellingPrice = 9.99m },
            new Product { Sku = "ELEC-003", Name = "Power Bank 20000mAh", CategoryId = electronics.Id, UnitOfMeasureId = EachUomId, TaxId = VatTaxId, CostPrice = 22.00m, SellingPrice = 49.99m },
            new Product { Sku = "GROC-001", Name = "Basmati Rice 5kg", CategoryId = groceries.Id, UnitOfMeasureId = CartonUomId, TaxId = VatTaxId, CostPrice = 12.50m, SellingPrice = 19.99m },
            new Product { Sku = "GROC-002", Name = "Canned Tuna", CategoryId = groceries.Id, UnitOfMeasureId = EachUomId, TaxId = VatTaxId, CostPrice = 1.20m, SellingPrice = 2.50m },
            new Product { Sku = "GROC-003", Name = "Cooking Oil 2L", CategoryId = groceries.Id, UnitOfMeasureId = LitreUomId, TaxId = VatTaxId, CostPrice = 4.80m, SellingPrice = 7.99m },
            new Product { Sku = "BEV-001", Name = "Fiji Water 1.5L (case)", CategoryId = beverages.Id, UnitOfMeasureId = CartonUomId, TaxId = VatTaxId, CostPrice = 8.00m, SellingPrice = 14.99m },
            new Product { Sku = "BEV-002", Name = "Coca-Cola 2L", CategoryId = beverages.Id, UnitOfMeasureId = LitreUomId, TaxId = VatTaxId, CostPrice = 1.80m, SellingPrice = 3.49m },
            new Product { Sku = "BEV-003", Name = "Green Tea 500ml", CategoryId = beverages.Id, UnitOfMeasureId = EachUomId, TaxId = VatTaxId, CostPrice = 1.50m, SellingPrice = 3.99m });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCustomersAsync(ApplicationDbContext db)
    {
        db.Customers.AddRange(
            new Customer { Code = "CUST-001", Name = "ABC Trading Ltd", Email = "orders@abctrading.com.fj", Phone = "+679 321 4567", AddressLine1 = "45 Renwick Road", City = "Suva", Country = "Fiji", TaxIdentificationNumber = "50-01234-0-6", CreditLimit = 15000m },
            new Customer { Code = "CUST-002", Name = "Island Fresh Supermarket", Email = "buyer@islandfresh.com.fj", Phone = "+679 334 5678", AddressLine1 = "78 Namadi Heights", City = "Suva", Country = "Fiji", TaxIdentificationNumber = "50-05678-0-2", CreditLimit = 25000m },
            new Customer { Code = "CUST-003", Name = "Sunset Beach Resort", Email = "procurement@sunsetresort.com.fj", Phone = "+679 666 1234", AddressLine1 = "Coral Coast", City = "Sigatoka", Country = "Fiji", CreditLimit = 10000m },
            new Customer { Code = "CUST-004", Name = "Fiji Maritime Services", Email = "supply@fijimaritime.com.fj", Phone = "+679 323 4567", AddressLine1 = "12 Kings Wharf", City = "Suva", Country = "Fiji", CreditLimit = 5000m },
            new Customer { Code = "CUST-005", Name = "Vanua Fresh Produce", Email = "sales@vanuafresh.com.fj", Phone = "+679 998 7654", City = "Nadi", Country = "Fiji", CreditLimit = 8000m });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSuppliersAsync(ApplicationDbContext db)
    {
        db.Suppliers.AddRange(
            new Supplier { Code = "SUPP-001", Name = "Pacific Distributors Pty Ltd", Email = "sales@pacificdist.com.au", Phone = "+61 2 9876 5432", AddressLine1 = "1 Export Drive", City = "Brisbane", Country = "Australia" },
            new Supplier { Code = "SUPP-002", Name = "Fiji Beverages Ltd", Email = "orders@fijibrew.com.fj", Phone = "+679 992 3456", City = "Lautoka", Country = "Fiji" },
            new Supplier { Code = "SUPP-003", Name = "Global Electronics Supplies", Email = "info@globalelec.cn", Phone = "+86 755 1234 5678" },
            new Supplier { Code = "SUPP-004", Name = "Prime Agriculture Co-op", Email = "export@primeagri.co.nz", Phone = "+64 9 555 1234" });
        await db.SaveChangesAsync();
    }

    /// <summary>Opening stock. Each receipt creates a FIFO layer AND a ledger movement.</summary>
    private static async Task<Dictionary<string, InventoryItem>> SeedOpeningStockAsync(ApplicationDbContext db)
    {
        var products = await db.Products.ToDictionaryAsync(p => p.Sku);
        var openedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var stock = new Dictionary<string, InventoryItem>();

        foreach (var (sku, qty, reorder) in OpeningStock)
        {
            var product = products[sku];
            var item = new InventoryItem
            {
                ProductId = product.Id,
                WarehouseId = MainWarehouseId,
                ReorderLevel = reorder,
            };
            var totalCost = item.Receive(qty, product.CostPrice);
            db.InventoryItems.Add(item);
            AddMovement(db, product.Id, MovementType.Receipt, qty, product.CostPrice, totalCost, openedAt, "OPENING", "Opening stock");
            stock[sku] = item;
        }

        await db.SaveChangesAsync();
        return stock;
    }

    private static async Task SeedPurchasingAsync(ApplicationDbContext db, Dictionary<string, InventoryItem> stock)
    {
        var suppliers = await db.Suppliers.ToDictionaryAsync(s => s.Code);
        var products = await db.Products.ToDictionaryAsync(p => p.Sku);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var receivedAt = DateTimeOffset.UtcNow.AddDays(-10);

        // PO-000001: fully received (drives stock in + a goods receipt document).
        var po1 = new PurchaseOrder
        {
            Number = "PO-000001",
            SupplierId = suppliers["SUPP-001"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-12),
        };
        po1.AddLine(products["ELEC-001"].Id, 50, 45.00m);
        po1.AddLine(products["ELEC-003"].Id, 40, 22.00m);
        po1.Confirm();

        var receipt = new GoodsReceipt
        {
            Number = "GRN-000001",
            PurchaseOrderId = po1.Id,
            WarehouseId = MainWarehouseId,
            ReceivedDate = today.AddDays(-10),
            Notes = "Received in good condition",
        };

        foreach (var line in po1.Lines.ToList())
        {
            po1.ReceiveLine(line.Id, line.Quantity);

            // Receiving must actually add stock and write a ledger entry.
            var sku = products.First(p => p.Value.Id == line.ProductId).Key;
            var totalCost = stock[sku].Receive(line.Quantity, line.UnitCost);
            AddMovement(db, line.ProductId, MovementType.Receipt, line.Quantity, line.UnitCost, totalCost, receivedAt, po1.Number, "Goods receipt");

            receipt.Lines.Add(new GoodsReceiptLine
            {
                GoodsReceiptId = receipt.Id,
                PurchaseOrderLineId = line.Id,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
            });
        }

        // PO-000002: confirmed, awaiting delivery. PO-000003: still a draft.
        var po2 = new PurchaseOrder
        {
            Number = "PO-000002",
            SupplierId = suppliers["SUPP-002"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-3),
        };
        po2.AddLine(products["BEV-001"].Id, 200, 8.00m);
        po2.Confirm();

        var po3 = new PurchaseOrder
        {
            Number = "PO-000003",
            SupplierId = suppliers["SUPP-003"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-1),
        };
        po3.AddLine(products["ELEC-002"].Id, 500, 3.50m);

        db.PurchaseOrders.AddRange(po1, po2, po3);
        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSalesOrdersAsync(ApplicationDbContext db, Dictionary<string, InventoryItem> stock)
    {
        var customers = await db.Customers.ToDictionaryAsync(c => c.Code);
        var products = await db.Products.ToDictionaryAsync(p => p.Sku);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fulfilledAt = DateTimeOffset.UtcNow.AddDays(-7);

        // SO-000001: confirmed, awaiting fulfilment.
        var so1 = new SalesOrder
        {
            Number = "SO-000001",
            CustomerId = customers["CUST-001"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-10),
            Notes = "Priority delivery",
        };
        so1.AddLine(products["ELEC-001"].Id, 5, 89.99m);
        so1.AddLine(products["ELEC-002"].Id, 10, 9.99m);
        so1.Confirm();

        // SO-000002: fulfilled — so it must actually issue stock and write ledger entries.
        var so2 = new SalesOrder
        {
            Number = "SO-000002",
            CustomerId = customers["CUST-002"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-7),
        };
        so2.AddLine(products["GROC-001"].Id, 12, 19.99m);
        so2.AddLine(products["BEV-001"].Id, 6, 14.99m);
        so2.AddLine(products["GROC-002"].Id, 24, 2.50m);
        so2.Confirm();

        foreach (var line in so2.Lines)
        {
            var sku = products.First(p => p.Value.Id == line.ProductId).Key;
            var cogs = stock[sku].Issue(line.Quantity);
            AddMovement(db, line.ProductId, MovementType.Issue, line.Quantity, null, cogs, fulfilledAt, so2.Number, "Sales order fulfilment");
        }
        so2.MarkFulfilled();

        // SO-000003: draft. SO-000004: cancelled.
        var so3 = new SalesOrder
        {
            Number = "SO-000003",
            CustomerId = customers["CUST-003"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today,
            Notes = "Follow up on room allocation",
        };
        so3.AddLine(products["ELEC-003"].Id, 2, 49.99m);

        var so4 = new SalesOrder
        {
            Number = "SO-000004",
            CustomerId = customers["CUST-005"].Id,
            WarehouseId = MainWarehouseId,
            OrderDate = today.AddDays(-14),
            Notes = "Customer postponed order",
        };
        so4.AddLine(products["GROC-003"].Id, 10, 7.99m);
        so4.Cancel();

        db.SalesOrders.AddRange(so1, so2, so3, so4);
        await db.SaveChangesAsync();
    }

    private static async Task SeedInvoicesAndPaymentsAsync(ApplicationDbContext db)
    {
        var customers = await db.Customers.ToDictionaryAsync(c => c.Code);
        var products = await db.Products.ToDictionaryAsync(p => p.Sku);
        var orders = await db.SalesOrders.ToDictionaryAsync(o => o.Number);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentSeq = 0;

        // INV-000001 — raised from SO-000001, issued and unpaid.
        var inv1 = new Invoice
        {
            Number = "INV-000001",
            CustomerId = customers["CUST-001"].Id,
            SalesOrderId = orders["SO-000001"].Id,
            IssueDate = today.AddDays(-8),
            DueDate = today.AddDays(22),
        };
        inv1.AddLine(products["ELEC-001"].Id, "Wireless Headphones", 5, 89.99m, VatPercent);
        inv1.AddLine(products["ELEC-002"].Id, "USB-C Charging Cable", 10, 9.99m, VatPercent);
        inv1.Issue();

        // INV-000002 — raised from the fulfilled SO-000002, paid in full.
        var inv2 = new Invoice
        {
            Number = "INV-000002",
            CustomerId = customers["CUST-002"].Id,
            SalesOrderId = orders["SO-000002"].Id,
            IssueDate = today.AddDays(-5),
            DueDate = today.AddDays(25),
        };
        inv2.AddLine(products["GROC-001"].Id, "Basmati Rice 5kg", 12, 19.99m, VatPercent);
        inv2.AddLine(products["BEV-001"].Id, "Fiji Water 1.5L (case)", 6, 14.99m, VatPercent);
        inv2.AddLine(products["GROC-002"].Id, "Canned Tuna", 24, 2.50m, VatPercent);
        inv2.Issue();
        PayInvoice(db, inv2, inv2.Total, today.AddDays(-3), PaymentMethod.BankTransfer, "TT-88231", ref paymentSeq);

        // INV-000003 — issued, part paid.
        var inv3 = new Invoice
        {
            Number = "INV-000003",
            CustomerId = customers["CUST-003"].Id,
            IssueDate = today.AddDays(-2),
            DueDate = today.AddDays(28),
        };
        inv3.AddLine(products["ELEC-003"].Id, "Power Bank 20000mAh", 1, 49.99m, VatPercent);
        inv3.AddLine(products["ELEC-002"].Id, "USB-C Charging Cable", 2, 9.99m, VatPercent);
        inv3.Issue();
        PayInvoice(db, inv3, 40.00m, today.AddDays(-1), PaymentMethod.Cash, null, ref paymentSeq);

        // INV-000004 — still a draft.
        var inv4 = new Invoice
        {
            Number = "INV-000004",
            CustomerId = customers["CUST-004"].Id,
            IssueDate = today,
            DueDate = today.AddDays(30),
        };
        inv4.AddLine(products["BEV-002"].Id, "Coca-Cola 2L", 10, 3.49m, VatPercent);

        db.Invoices.AddRange(inv1, inv2, inv3, inv4);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSupplierInvoiceAsync(ApplicationDbContext db)
    {
        var supplier = await db.Suppliers.FirstAsync(s => s.Code == "SUPP-001");
        var po = await db.PurchaseOrders.FirstAsync(p => p.Number == "PO-000001");
        var products = await db.Products.ToDictionaryAsync(p => p.Sku);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var bill = new SupplierInvoice
        {
            Number = "SINV-000001",
            SupplierId = supplier.Id,
            PurchaseOrderId = po.Id,
            IssueDate = today.AddDays(-9),
            DueDate = today.AddDays(21),
            SupplierReference = "PD-INV-4471",
        };
        bill.AddLine(products["ELEC-001"].Id, "Wireless Headphones", 50, 45.00m, VatPercent);
        bill.AddLine(products["ELEC-003"].Id, "Power Bank 20000mAh", 40, 22.00m, VatPercent);
        bill.Approve();

        // Part payment, with a matching supplier-payment record.
        const decimal paid = 2000.00m;
        bill.ApplyPayment(paid);
        db.SupplierPayments.Add(new SupplierPayment
        {
            Number = "SPAY-000001",
            SupplierInvoiceId = bill.Id,
            SupplierId = supplier.Id,
            Amount = paid,
            PaymentDate = today.AddDays(-6),
            Method = PaymentMethod.BankTransfer,
            Reference = "TT-77104",
        });

        db.SupplierInvoices.Add(bill);
        await db.SaveChangesAsync();
    }

    /// <summary>Applies a payment to an invoice and records the matching Payment row.</summary>
    private static void PayInvoice(
        ApplicationDbContext db, Invoice invoice, decimal amount, DateOnly paidOn,
        PaymentMethod method, string? reference, ref int seq)
    {
        invoice.ApplyPayment(amount);
        db.Payments.Add(new Payment
        {
            Number = $"PAY-{++seq:D6}",
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Amount = amount,
            PaymentDate = paidOn,
            Method = method,
            Reference = reference,
        });
    }

    /// <summary>Appends an immutable ledger entry so stock levels are always explained by movements.</summary>
    private static void AddMovement(
        ApplicationDbContext db, Guid productId, MovementType type, decimal quantity,
        decimal? unitCost, decimal totalCost, DateTimeOffset occurredAt, string? reference, string? notes)
        => db.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            WarehouseId = MainWarehouseId,
            Type = type,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = totalCost,
            OccurredAt = occurredAt,
            Reference = reference,
            Notes = notes,
        });
}
