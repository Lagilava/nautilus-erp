using ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Persistence;

/// <summary>
/// Lets the EF Core tools (migrations) construct the context at design time without the
/// full DI graph. Uses a local SQL Server connection and no-op ambient services — these
/// are never used to run the app, only to build the model.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ERP;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True;")
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
    }

    private sealed class DesignTimeClock : IDateTime
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
