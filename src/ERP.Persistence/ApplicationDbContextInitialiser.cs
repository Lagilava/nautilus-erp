using ERP.Application.Common.Services;
using ERP.Domain.Accounting;
using ERP.Persistence.Identity;
using ERP.Shared.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Persistence;

/// <summary>
/// Applies migrations and seeds baseline data (roles + a bootstrap administrator) so a
/// fresh deployment is usable. Idempotent: safe to run on every startup.
/// </summary>
public sealed class ApplicationDbContextInitialiser
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<ApplicationDbContextInitialiser> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task MigrateAsync()
    {
        // SQL Server and Postgres each have a real migration set, so their schema evolves through
        // versioned migrations. SQLite (local dev) and the in-memory provider (tests) build the
        // schema directly from the model — they are throwaway databases, not deployments.
        var migrated = _db.Database.ProviderName is "Microsoft.EntityFrameworkCore.SqlServer"
            or "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (migrated)
            await _db.Database.MigrateAsync();
        else
            await _db.Database.EnsureCreatedAsync();
    }

    public async Task SeedAsync()
    {
        if (_db.Database.IsSqlite() && await IsSqliteSchemaIncompleteAsync())
        {
            _logger.LogWarning(
                "SQLite schema was incomplete during startup seed; recreating the local database before continuing.");

            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
        }

        await SeedCoreAsync();
    }

    private async Task SeedCoreAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        // Ensure the single company-profile row exists so invoices always have seller details.
        if (!await _db.CompanyProfiles.AnyAsync())
        {
            _db.CompanyProfiles.Add(new Domain.Organization.CompanyProfile { Id = Domain.Organization.CompanyProfile.SingletonId });
            await _db.SaveChangesAsync();
        }

        await SeedChartOfAccountsAsync();

        var adminEmail = _configuration["Seed:AdminEmail"];
        var adminPassword = _configuration["Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword)
            && await _userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Administrator"
            };

            var created = await _userManager.CreateAsync(admin, adminPassword);
            if (created.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, Roles.Administrator);
                _logger.LogInformation("Seeded bootstrap administrator {Email}", adminEmail);
            }
            else
            {
                _logger.LogWarning("Failed to seed administrator: {Errors}",
                    string.Join(", ", created.Errors.Select(e => e.Description)));
            }
        }
    }


    /// <summary>
    /// Seeds the system accounts every auto-posting handler relies on. Runs on every startup
    /// (idempotent via the Code check), independent of the optional demo dataset, because
    /// auto-posting must work even in a bare deployment with no demo data seeded.
    /// </summary>
    private async Task SeedChartOfAccountsAsync()
    {
        (string Code, string Name, AccountType Type)[] systemAccounts =
        [
            (GeneralLedgerAccountCodes.Cash, "Cash", AccountType.Asset),
            (GeneralLedgerAccountCodes.AccountsReceivable, "Accounts Receivable", AccountType.Asset),
            (GeneralLedgerAccountCodes.Inventory, "Inventory", AccountType.Asset),
            (GeneralLedgerAccountCodes.AccountsPayable, "Accounts Payable", AccountType.Liability),
            (GeneralLedgerAccountCodes.SalesTaxPayable, "Sales Tax Payable", AccountType.Liability),
            (GeneralLedgerAccountCodes.SalesRevenue, "Sales Revenue", AccountType.Revenue),
            (GeneralLedgerAccountCodes.CostOfGoodsSold, "Cost of Goods Sold", AccountType.Expense),
        ];

        var existingCodes = await _db.Accounts.Select(a => a.Code).ToListAsync();
        foreach (var (code, name, type) in systemAccounts)
        {
            if (existingCodes.Contains(code)) continue;
            _db.Accounts.Add(new Account { Code = code, Name = name, Type = type, IsSystem = true, IsActive = true });
        }
        await _db.SaveChangesAsync();
    }

    private async Task<bool> IsSqliteSchemaIncompleteAsync()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            return !await TableExistsAsync(connection, "CompanyProfiles")
                   || !await TableExistsAsync(connection, "AspNetUsers")
                   || !await TableExistsAsync(connection, "AspNetRoles");
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }
}
