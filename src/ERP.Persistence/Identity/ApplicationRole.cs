using Microsoft.AspNetCore.Identity;

namespace ERP.Persistence.Identity;

/// <summary>Role entity with GUID keys, matching the user's key type.</summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
