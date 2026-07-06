using System.Linq;
using ERP.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>
/// Boots the real API host but swaps SQL Server for the EF Core in-memory provider, so
/// the full request pipeline (auth, MediatR, validation, Identity) is exercised without
/// external infrastructure. Each factory instance gets an isolated database.
/// </summary>
public sealed class ErpWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"erp-tests-{Guid.NewGuid()}";

    /// <summary>Bootstrap admin seeded for tests that exercise Manager/Administrator-only endpoints.</summary>
    public const string AdminEmail = "admin@erp.test";
    public const string AdminPassword = "Admin#12345";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Disable admin seeding in tests. We deliberately do NOT override the Jwt
            // section: the JWT validation parameters are captured at service-registration
            // time (from appsettings.json), while token *signing* reads IOptions at runtime.
            // Overriding here lands after registration, so signer and validator would use
            // different keys. Reusing appsettings.json keeps both sides consistent.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword
            });
        });

        builder.ConfigureServices(services =>
        {
            // Strip every registration tied to the SQL Server DbContext — in EF 9 the
            // provider (UseSqlServer) is carried by IDbContextOptionsConfiguration<T>, so
            // removing only DbContextOptions<T> leaves the provider applied and EF refuses
            // to register a second (in-memory) one. Remove them all, then re-add in-memory.
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }
}
