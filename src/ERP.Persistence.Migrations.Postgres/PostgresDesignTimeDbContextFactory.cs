using ERP.Application.Common.Interfaces;
using ERP.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Persistence.Migrations.Postgres;

/// <summary>
/// The EF tools look for a context (or a factory) in the <em>startup</em> assembly, and this
/// project is its own startup so that scaffolding never depends on the API. The connection is
/// never opened — only the model is built — but the provider must be Npgsql so the emitted DDL
/// is Postgres DDL.
///
/// <code>
/// dotnet ef migrations add Name \
///   -p src/ERP.Persistence.Migrations.Postgres -s src/ERP.Persistence.Migrations.Postgres
/// </code>
/// </summary>
public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=ERP;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(MigrationsAssemblies.Postgres))
            .Options;

        return new ApplicationDbContext(options, new DesignTimeCurrentUser(), new DesignTimeClock());
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
