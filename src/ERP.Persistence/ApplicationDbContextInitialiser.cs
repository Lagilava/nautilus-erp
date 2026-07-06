using ERP.Persistence.Identity;
using ERP.Shared.Authorization;
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
        // Only relational providers migrate; the in-memory provider used in tests does not.
        if (_db.Database.IsRelational())
            await _db.Database.MigrateAsync();
        else
            await _db.Database.EnsureCreatedAsync();
    }

    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new ApplicationRole(role));
        }

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
}
