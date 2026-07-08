using ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Persistence;

/// <summary>
/// Lets the EF Core tools (migrations) construct the context at design time without the
/// full DI graph. The connections below are never opened to run the app, only to build the
/// model — but the provider must match the migration set being scaffolded, because the two
/// engines emit different DDL.
///
/// <code>
/// # SQL Server (default), set lives in ERP.Persistence:
/// dotnet ef migrations add Name -p src/ERP.Persistence -s src/ERP.Persistence
///
/// # Postgres, set lives in its own project:
/// ERP_MIGRATIONS_PROVIDER=Postgres dotnet ef migrations add Name \
///   -p src/ERP.Persistence.Migrations.Postgres -s src/ERP.Persistence
/// </code>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("ERP_MIGRATIONS_PROVIDER") ?? "SqlServer";
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            builder.UseNpgsql(
                "Host=localhost;Port=5432;Database=ERP;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(MigrationsAssemblies.Postgres));
        else
            builder.UseSqlServer(
                "Server=localhost,1433;Database=ERP;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True;");

        return new ApplicationDbContext(builder.Options, new DesignTimeCurrentUser(), new DesignTimeClock());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
        public string? IpAddress => null;
        public string? UserAgent => null;
        public Guid? BranchId => null;
    }

    private sealed class DesignTimeClock : IDateTime
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
