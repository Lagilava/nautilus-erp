using Microsoft.AspNetCore.Identity;

namespace ERP.Persistence.Identity;

/// <summary>
/// The persisted identity user. Inherits ASP.NET Identity (a framework/persistence
/// concern), which is exactly why it lives in Persistence and not in the Domain.
/// GUID keys align with the project-wide PK convention.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Branch this user is scoped to. Null means unrestricted — the user sees every branch.
    /// Drives record-level security on warehouse-bound data (stock, sales, purchasing).
    /// </summary>
    public Guid? BranchId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
