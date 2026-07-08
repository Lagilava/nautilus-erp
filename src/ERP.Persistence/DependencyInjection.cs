using ERP.Application.Common.Interfaces;
using ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Persistence;

/// <summary>Registers EF Core, the DbContext, ASP.NET Identity stores, and the identity service.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Provider is config-driven so local dev can run on file-based SQLite with zero
        // infrastructure, while SQL Server remains the default engine and Postgres is available
        // for platforms (Render, Fly, Heroku) that only offer managed Postgres.
        var provider = configuration["Database:Provider"] ?? "SqlServer";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqlite = configuration.GetConnectionString("Sqlite") ?? "Data Source=erp.db";
                options.UseSqlite(sqlite);
            }
            else if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
                     || provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                // Migrations differ per engine (identity columns, rowversion, index syntax), so
                // each provider gets its own set in its own assembly. See MigrationsAssemblies.
                var postgres = PostgresConnectionString.Normalize(
                    configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
                options.UseNpgsql(postgres, npgsql => npgsql
                    .EnableRetryOnFailure()
                    .MigrationsAssembly(MigrationsAssemblies.Postgres));
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            }
        });

        return services.AddPersistenceCore();
    }

    /// <summary>
    /// Registers everything except the DbContext provider, so tests can plug in an
    /// in-memory context while sharing the same Identity + service wiring.
    /// </summary>
    public static IServiceCollection AddPersistenceCore(this IServiceCollection services)
    {
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;

                // Account lockout: 5 failed attempts → 15 minute lockout.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ApplicationDbContextInitialiser>();

        return services;
    }
}
