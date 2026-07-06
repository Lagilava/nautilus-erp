using System.Linq;
using System.Reflection;

namespace ERP.UnitTests;

/// <summary>
/// Enforces the non-negotiable dependency direction at test time so a future
/// milestone cannot quietly make the Domain depend on Application/Infrastructure.
/// </summary>
public class ArchitectureSanityTests
{
    [Fact]
    public void Domain_does_not_reference_application_infrastructure_or_persistence()
    {
        var domain = typeof(ERP.Domain.AssemblyMarker).Assembly;

        var referenced = domain.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        Assert.DoesNotContain("ERP.Application", referenced);
        Assert.DoesNotContain("ERP.Infrastructure", referenced);
        Assert.DoesNotContain("ERP.Persistence", referenced);
    }
}
